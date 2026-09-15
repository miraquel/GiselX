using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Tests;

/// <summary>
/// Captures what an upload would have notified instead of queueing a background job, so a test can
/// assert both that a notification was raised and what it said.
/// </summary>
public sealed class RecordingUploadNotificationDispatcher : IUploadNotificationDispatcher
{
    public List<UploadNotificationDto> Enqueued { get; } = [];

    public void Enqueue(UploadNotificationDto notification) => Enqueued.Add(notification);
}

