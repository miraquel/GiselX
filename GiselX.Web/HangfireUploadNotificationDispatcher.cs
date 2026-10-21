using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Hangfire;

namespace GiselX.Web;

/// <summary>
/// Queues the completion mail as a Hangfire job. The upload services call this after their
/// transaction has committed, so the job can never observe rows that were rolled back, and SMTP
/// latency or an unreachable mail server never delays or fails the upload request itself.
/// </summary>
public sealed class HangfireUploadNotificationDispatcher(
    IBackgroundJobClient backgroundJobs,
    ILogger<HangfireUploadNotificationDispatcher> logger) : IUploadNotificationDispatcher
{
    public void Enqueue(UploadNotificationDto notification)
    {
        try
        {
            backgroundJobs.Enqueue<IUploadNotificationService>(service =>
                service.NotifyUploadCompletedAsync(notification, CancellationToken.None));
        }
        catch (Exception exception)
        {
            // The upload is already committed and the user has been told it succeeded. Losing the
            // notification is a lesser failure than turning a successful upload into an error page.
            logger.LogError(exception,
                "Could not queue the {Kind} upload notification for company {CompanyId}.",
                notification.Kind, notification.CompanyId);
        }
    }
}
