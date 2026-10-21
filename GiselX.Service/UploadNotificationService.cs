using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.Extensions.Logging;

namespace GiselX.Service;

/// <summary>
/// Turns a committed upload into the completion mail. This runs in a background job, so it owns the
/// decision of whom to tell: the distributor's own contact address, and the internal PT. GMK list
/// configured on the company's Business Unit.
/// </summary>
public sealed class UploadNotificationService(
    IUploadNotificationRepository repository,
    IEmailService emailService,
    IEmailSettingsProvider settingsProvider,
    ILogger<UploadNotificationService> logger) : IUploadNotificationService
{
    public async Task NotifyUploadCompletedAsync(UploadNotificationDto notification,
        CancellationToken cancellationToken)
    {
        // A blank SmtpHost is the documented "email is not set up here" state (it ships blank in
        // appsettings.json). Retrying that ten times per upload would bury the real failures, so
        // this returns instead of throwing and lets Hangfire mark the job done.
        var settings = await settingsProvider.GetAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(settings.SmtpHost))
        {
            logger.LogWarning(
                "Upload notification for company {CompanyId} skipped: no SMTP host is configured.",
                notification.CompanyId);
            return;
        }

        var recipients = await repository.GetRecipientsAsync(notification.CompanyId, cancellationToken);
        if (recipients is null)
        {
            logger.LogWarning(
                "Upload notification skipped: company {CompanyId} no longer exists.", notification.CompanyId);
            return;
        }

        var distributor = NotificationRecipients.Parse(recipients.ContactEmail);

        // The internal side is assembled from three masters: the Business Unit, the Channel and the
        // Salesman accountable for this distributor. They overlap in practice — one person can be
        // listed on a unit and on their own salesman record — so the combined list is de-duplicated
        // rather than mailing them twice.
        var internalTeam = NotificationRecipients.Combine(
            recipients.InternalNotificationEmails,
            recipients.ChannelNotificationEmails,
            recipients.SalesmanNotificationEmails);

        if (distributor.Count == 0 && internalTeam.Count == 0)
        {
            // Worth saying out loud: the upload succeeded and nobody was told, which looks like a
            // broken notification until you know the master data is simply empty. Naming all four
            // sources saves the reader guessing which one to go and fill in.
            logger.LogWarning(
                "Upload notification for {Company} ({Kind}) sent to nobody: the company has no " +
                "ContactEmail, and neither business unit '{BusinessUnit}', channel '{Channel}' nor " +
                "salesman '{Salesman}' has notification emails.",
                recipients.CompanyName, notification.Kind,
                recipients.BusinessUnitName ?? "(none)",
                recipients.ChannelName ?? "(none)",
                recipients.SalesmanName ?? "(none)");
            return;
        }

        await emailService.SendUploadCompletedAsync(
            notification, recipients.CompanyName, distributor, internalTeam, cancellationToken);

        logger.LogInformation(
            "Upload notification for {Company} ({Kind}) sent to {DistributorCount} distributor and " +
            "{InternalCount} internal recipient(s).",
            recipients.CompanyName, notification.Kind, distributor.Count, internalTeam.Count);
    }
}
