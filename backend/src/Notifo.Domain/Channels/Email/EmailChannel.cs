﻿// ==========================================================================
//  Notifo.io
// ==========================================================================
//  Copyright (c) Sebastian Stehle
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Diagnostics;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Notifo.Domain.Apps;
using Notifo.Domain.ChannelTemplates;
using Notifo.Domain.Integrations;
using Notifo.Domain.Log;
using Notifo.Domain.Resources;
using Notifo.Domain.UserNotifications;
using Notifo.Domain.Users;
using Notifo.Infrastructure;
using Notifo.Infrastructure.Scheduling;
using IEmailTemplateStore = Notifo.Domain.ChannelTemplates.IChannelTemplateStore<Notifo.Domain.Channels.Email.EmailTemplate>;
using IUserNotificationQueue = Notifo.Infrastructure.Scheduling.IScheduler<Notifo.Domain.Channels.Email.EmailJob>;

namespace Notifo.Domain.Channels.Email
{
    public sealed class EmailChannel : ICommunicationChannel, IScheduleHandler<EmailJob>
    {
        private readonly IAppStore appStore;
        private readonly IEmailFormatter emailFormatter;
        private readonly IEmailTemplateStore emailTemplateStore;
        private readonly IIntegrationManager integrationManager;
        private readonly ILogger<EmailChannel> log;
        private readonly ILogStore logStore;
        private readonly IUserNotificationQueue userNotificationQueue;
        private readonly IUserNotificationStore userNotificationStore;
        private readonly IUserStore userStore;

        public string Name => Providers.Email;

        public EmailChannel(ILogger<EmailChannel> log, ILogStore logStore,
            IAppStore appStore,
            IIntegrationManager integrationManager,
            IEmailFormatter emailFormatter,
            IEmailTemplateStore emailTemplateStore,
            IUserNotificationQueue userNotificationQueue,
            IUserNotificationStore userNotificationStore,
            IUserStore userStore)
        {
            this.appStore = appStore;
            this.emailFormatter = emailFormatter;
            this.emailTemplateStore = emailTemplateStore;
            this.log = log;
            this.logStore = logStore;
            this.integrationManager = integrationManager;
            this.userNotificationQueue = userNotificationQueue;
            this.userNotificationStore = userNotificationStore;
            this.userStore = userStore;
        }

        public IEnumerable<string> GetConfigurations(UserNotification notification, ChannelSetting settings, SendOptions options)
        {
            if (!integrationManager.IsConfigured<IEmailSender>(options.App, notification))
            {
                yield break;
            }

            if (notification.Silent || string.IsNullOrEmpty(options.User.EmailAddress))
            {
                yield break;
            }

            yield return options.User.EmailAddress;
        }

        public async Task SendAsync(UserNotification notification, ChannelSetting setting, string configuration, SendOptions options,
            CancellationToken ct)
        {
            if (options.IsUpdate)
            {
                return;
            }

            using (Telemetry.Activities.StartActivity("EmailChannel/SendAsync"))
            {
                var job = new EmailJob(notification, setting, configuration);

                await userNotificationQueue.ScheduleGroupedAsync(
                    job.ScheduleKey,
                    job,
                    job.Delay,
                    false, ct);
            }
        }

        public async Task<bool> HandleAsync(List<EmailJob> jobs, bool isLastAttempt,
            CancellationToken ct)
        {
            var links = jobs.SelectMany(x => x.Notification.Links());

            var parentContext = Activity.Current?.Context ?? default;

            using (Telemetry.Activities.StartActivity("EmailChannel/Handle", ActivityKind.Internal, parentContext, links: links))
            {
                var unhandledJobs = new List<EmailJob>();

                foreach (var job in jobs)
                {
                    if (await userNotificationStore.IsHandledAsync(job, this, ct))
                    {
                        await UpdateAsync(job.Notification, job.EmailAddress, ProcessStatus.Skipped);
                    }
                    else
                    {
                        unhandledJobs.Add(job);
                    }
                }

                if (unhandledJobs.Any())
                {
                    await SendJobsAsync(unhandledJobs, ct);
                }

                return true;
            }
        }

        public Task HandleExceptionAsync(List<EmailJob> jobs, Exception ex)
        {
            return UpdateAsync(jobs, jobs[0].EmailAddress, ProcessStatus.Failed);
        }

        public async Task SendJobsAsync(List<EmailJob> jobs,
            CancellationToken ct)
        {
            using (Telemetry.Activities.StartActivity("Send"))
            {
                var first = jobs[0];

                var commonEmail = first.EmailAddress;
                var commonApp = first.Notification.AppId;
                var commonUser = first.Notification.UserId;

                await UpdateAsync(first.Notification, commonEmail, ProcessStatus.Attempt);

                var app = await appStore.GetCachedAsync(first.Notification.AppId, ct);

                if (app == null)
                {
                    log.LogWarning("Cannot send email: App not found.");

                    await UpdateAsync(jobs, commonEmail, ProcessStatus.Handled);
                    return;
                }

                try
                {
                    var user = await userStore.GetCachedAsync(commonApp, commonUser, ct);

                    if (user == null)
                    {
                        await SkipAsync(jobs, commonEmail, Texts.Email_UserDeleted);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(user.EmailAddress))
                    {
                        await SkipAsync(jobs, commonEmail, Texts.Email_UserNoEmail);
                        return;
                    }

                    var senders = integrationManager.Resolve<IEmailSender>(app, first.Notification).Select(x => x.Target).ToList();

                    if (senders.Count == 0)
                    {
                        await SkipAsync(jobs, commonEmail, Texts.Email_ConfigReset);
                        return;
                    }

                    EmailMessage? message;

                    using (Telemetry.Activities.StartActivity("Format"))
                    {
                        var (skip, template) = await GetTemplateAsync(
                            first.Notification.AppId,
                            first.Notification.UserLanguage,
                            first.EmailTemplate,
                            ct);

                        if (skip != null)
                        {
                            await SkipAsync(jobs, commonEmail, skip!);
                            return;
                        }

                        if (template == null)
                        {
                            return;
                        }

                        var (result, errors) = await emailFormatter.FormatAsync(jobs, template, app, user, false, ct);

                        if (errors.Count > 0 || result == null)
                        {
                            throw new EmailFormattingException(errors);
                        }

                        message = result;
                    }

                    await SendCoreAsync(message, app.Id, senders, ct);

                    await UpdateAsync(jobs, commonEmail, ProcessStatus.Handled);
                }
                catch (DomainException ex)
                {
                    await logStore.LogAsync(app.Id, Name, ex.Message);
                    throw;
                }
            }
        }

        private async Task SendCoreAsync(EmailMessage message, string appId, List<IEmailSender> senders,
            CancellationToken ct)
        {
            var lastSender = senders[^1];

            foreach (var sender in senders)
            {
                try
                {
                    await sender.SendAsync(message, ct);
                    return;
                }
                catch (DomainException ex)
                {
                    await logStore.LogAsync(appId, Name, ex.Message);

                    if (sender == lastSender)
                    {
                        throw;
                    }
                }
                catch (Exception)
                {
                    if (sender == lastSender)
                    {
                        throw;
                    }
                }
            }
        }

        private Task UpdateAsync(IUserNotification notification, string email, ProcessStatus status, string? reason = null)
        {
            return userNotificationStore.CollectAndUpdateAsync(notification, Name, email, status, reason);
        }

        private async Task SkipAsync(List<EmailJob> jobs, string email, string reason)
        {
            await logStore.LogAsync(jobs[0].Notification.AppId, Name, reason);

            await UpdateAsync(jobs, email, ProcessStatus.Skipped);
        }

        private async Task UpdateAsync(List<EmailJob> jobs, string email, ProcessStatus status, string? reason = null)
        {
            foreach (var job in jobs)
            {
                await UpdateAsync(job.Notification, email, status, reason);
            }
        }

        private async Task<(string? Skip, EmailTemplate?)> GetTemplateAsync(
            string appId,
            string language,
            string? name,
            CancellationToken ct)
        {
            var (status, template) = await emailTemplateStore.GetBestAsync(appId, name, language, ct);

            switch (status)
            {
                case TemplateResolveStatus.ResolvedWithFallback:
                    {
                        var error = string.Format(CultureInfo.InvariantCulture, Texts.ChannelTemplate_ResolvedWithFallback, name ?? "Unnamed");

                        await logStore.LogAsync(appId, Name, error);
                        break;
                    }

                case TemplateResolveStatus.NotFound:
                    {
                        var error = string.Format(CultureInfo.InvariantCulture, Texts.ChannelTemplate_NotFound, name ?? "Unnamed");

                        return (error, null);
                    }

                case TemplateResolveStatus.LanguageNotFound:
                    {
                        var error = string.Format(CultureInfo.InvariantCulture, Texts.ChannelTemplate_LanguageNotFound, language, name ?? "Unnamed");

                        return (error, null);
                    }
            }

            return (null, template);
        }
    }
}
