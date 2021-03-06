// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Web;
using Microsoft.Extensions.Logging;
using NuGetGallery.Services;
using NuGet.Services.Entities;

namespace NuGetGallery
{
    public class CookieBasedABTestService : IABTestService
    {
        private const string CookieName = "nugetab";

        private readonly HttpContextBase _httpContext;
        private readonly IFeatureFlagService _featureFlagService;
        private readonly IABTestEnrollmentFactory _enrollmentFactory;
        private readonly IContentObjectService _contentObjectService;
        private readonly ITelemetryService _telemetryService;
        private readonly ILogger<CookieBasedABTestService> _logger;
        private readonly Lazy<ABTestEnrollment> _lazyEnrollment;
        private readonly IDateTimeProvider _dateTimeProvider;

        public CookieBasedABTestService(
            HttpContextBase httpContext,
            IFeatureFlagService featureFlagService,
            IABTestEnrollmentFactory enrollmentFactory,
            IContentObjectService contentObjectService,
            ITelemetryService telemetryService,
            ILogger<CookieBasedABTestService> logger,
            IDateTimeProvider dateTimeProvider)
        {
            _httpContext = httpContext ?? throw new ArgumentNullException(nameof(httpContext));
            _featureFlagService = featureFlagService ?? throw new ArgumentNullException(nameof(featureFlagService));
            _enrollmentFactory = enrollmentFactory ?? throw new ArgumentNullException(nameof(enrollmentFactory));
            _contentObjectService = contentObjectService ?? throw new ArgumentNullException(nameof(contentObjectService));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _lazyEnrollment = new Lazy<ABTestEnrollment>(DetermineEnrollment);
            _dateTimeProvider = dateTimeProvider ?? throw new ArgumentNullException(nameof(dateTimeProvider));
        }

        public bool IsPreviewSearchEnabled(User user)
        {
            return IsActive(
                nameof(Enrollment.PreviewSearchBucket),
                user,
                enrollment => enrollment.PreviewSearchBucket,
                config => config.PreviewSearchPercentage);
        }

        private ABTestEnrollment Enrollment => _lazyEnrollment.Value;
        private IABTestConfiguration Config => _contentObjectService.ABTestConfiguration;

        private ABTestEnrollment DetermineEnrollment()
        {
            // Try to read the cookie from the request cookies.
            ABTestEnrollment enrollment;
            var requestCookie = _httpContext.Request.Cookies[CookieName];
            if (requestCookie == null || string.IsNullOrWhiteSpace(requestCookie.Value))
            {
                // There is no A/B test cookie at all. Initialize one.
                enrollment = _enrollmentFactory.Initialize();
            }
            else if (!_enrollmentFactory.TryDeserialize(requestCookie.Value, out enrollment))
            {
                // The A/B test cookie could not be deserialized.
                enrollment = _enrollmentFactory.Initialize();
                _logger.LogWarning("An A/B test cookie could not be deserialized: {Value}", requestCookie.Value);
            }

            if (enrollment.State == ABTestEnrollmentState.Upgraded || enrollment.State == ABTestEnrollmentState.FirstHit)
            {
                var responseCookie = new HttpCookie(CookieName)
                {
                    HttpOnly = true,
                    Secure = true,
                    Value = _enrollmentFactory.Serialize(enrollment),
                    Expires = _dateTimeProvider.UtcNow.AddYears(1),
                };
                _httpContext.Response.Cookies.Add(responseCookie);
            }

            return enrollment;
        }

        private bool IsActive(
            string name,
            User user,
            Func<ABTestEnrollment, int> getTestBucket,
            Func<IABTestConfiguration, int> getTestPercentage)
        {
            var isAuthenticated = user != null;
            var authStatus = isAuthenticated ? "authenticated" : "anonymous";
            const string inactive = "inactive";
            const string active = "active";

            if (!_featureFlagService.IsABTestingEnabled(user))
            {
                _logger.LogInformation(
                    "A/B test {Name} is {TestStatus} for an {AuthStatus} user due to the general A/B testing " +
                    "feature flag.",
                    name,
                    inactive,
                    authStatus);

                return false;
            }

            var testBucket = getTestBucket(Enrollment);
            var testPercentage = getTestPercentage(Config);
            var isActive = testBucket <= testPercentage;

            _telemetryService.TrackABTestEvaluated(
                name,
                isActive,
                isAuthenticated,
                testBucket,
                testPercentage);
            _logger.LogInformation(
                "A/B test {Name} is {TestStatus} for an {AuthStatus} user due to enrollment value " +
                "{EnrollmentValue} and config value {ConfigValue}.",
                name,
                isActive ? active : inactive,
                authStatus,
                testBucket,
                testPercentage);

            return isActive;
        }
    }
}