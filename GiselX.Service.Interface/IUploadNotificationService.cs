using GiselX.Service.Dto;

namespace GiselX.Service.Interface;

/// <summary>
/// Resolves who should hear about a completed upload and sends them the mail. Runs in a background
/// job, outside the request that performed the upload.
/// </summary>
public interface IUploadNotificationService
{
    Task NotifyUploadCompletedAsync(UploadNotificationDto notification, CancellationToken cancellationToken);
}
