// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;

namespace NuGetGallery
{
    /// <summary>
    /// Because we have web.config based redirects and other steps in the pipeline that act before ASP.NET determines
    /// the controller and action that a URL is associated with, the "operation name" field has an extremely high
    /// cardinality which makes it inappropriate as a dimension in some metric systems. For example, sometimes the
    /// operation name is the verbatim URL path, with parameters filled in. Therefore, we have a list of known operation
    /// names that we copy to another field, which is better for aggregation.
    /// </summary>
    public class KnownOperationNameEnricher : ITelemetryInitializer
    {
        private const string KnownOperation = "KnownOperation";

        private static readonly HashSet<string> KnownOperations = new HashSet<string>
        {
            "GET Packages/DisplayPackage",
        };

        public void Initialize(ITelemetry telemetry)
        {
            var request = telemetry as RequestTelemetry;
            if (request == null)
            {
                return;
            }

            var itemTelemetry = telemetry as ISupportProperties;
            if (itemTelemetry == null)
            {
                return;
            }

            var operationName = telemetry.Context?.Operation?.Name;
            if (operationName == null)
            {
                return;
            }

            if (KnownOperations.Contains(operationName))
            {
                itemTelemetry.Properties[KnownOperation] = operationName;
            }
        }
    }
}