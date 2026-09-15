using GiselX.Service.Dto;

namespace GiselX.Service.Interface;

/// <summary>
/// Hands a completed upload to whatever sends the mail later. The upload services depend on this
/// rather than on Hangfire directly, so the Service layer keeps no background-job dependency; the
/// Web layer supplies the implementation that enqueues the job.
///
/// Implementations must not throw: a notification that cannot be queued is never a reason to fail
/// an upload that has already been committed.
/// </summary>
public interface IUploadNotificationDispatcher
{
    void Enqueue(UploadNotificationDto notification);
}
