// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Globalization;
using System.Net.Mail;
using NuGet.Services.Entities;
using NuGet.Services.Messaging.Email;
using NuGetGallery.Authentication;

namespace NuGetGallery.Infrastructure.Mail.Messages
{
    public class CredentialRemovedMessage : MarkdownEmailBuilder
    {
        private readonly IMessageServiceConfiguration _configuration;

        public CredentialRemovedMessage(
            IMessageServiceConfiguration configuration,
            User user,
            CredentialTypeInfo credentialType)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            User = user ?? throw new ArgumentNullException(nameof(user));
            CredentialType = credentialType ?? throw new ArgumentNullException(nameof(credentialType));
        }

        public override MailAddress Sender => _configuration.GalleryOwner;

        public CredentialTypeInfo CredentialType { get; }

        public User User { get; }

        public override IEmailRecipients GetRecipients()
            => new EmailRecipients(
                to: new[] { User.ToMailAddress() });

        public override string GetSubject()
            => string.Format(
                CultureInfo.CurrentCulture,
                Strings.Emails_CredentialRemoved_Subject,
                _configuration.GalleryOwner.DisplayName,
                CredentialType.IsApiKey ? Strings.CredentialType_ApiKey : CredentialType.Description);

        protected override string GetMarkdownBody()
            => string.Format(
                CultureInfo.CurrentCulture,
                CredentialType.IsApiKey ? Strings.Emails_ApiKeyRemoved_Body : Strings.Emails_CredentialRemoved_Body,
                CredentialType.Description);
    }
}
