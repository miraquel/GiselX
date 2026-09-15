using GiselX.Service.Dto;
using GiselX.Web;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;

namespace GiselX.Tests;

public sealed class HangfireUploadNotificationDispatcherTests
{
    [Fact]
    public void A_notification_is_queued_as_a_background_job()
    {
        var client = new RecordingBackgroundJobClient();
        var dispatcher = new HangfireUploadNotificationDispatcher(
            client, NullLogger<HangfireUploadNotificationDispatcher>.Instance);

        dispatcher.Enqueue(new UploadNotificationDto { CompanyId = 7, Kind = UploadNotificationKind.Sellout });

        var job = Assert.Single(client.Created);
        Assert.Equal(nameof(GiselX.Service.Interface.IUploadNotificationService.NotifyUploadCompletedAsync),
            job.Method.Name);
    }

    [Fact]
    public void A_queue_that_is_unavailable_is_swallowed_rather_than_thrown()
    {
        // By the time this runs the upload has committed and the user is being told it succeeded.
        // Surfacing a job-storage outage here would turn a successful upload into an error page.
        var dispatcher = new HangfireUploadNotificationDispatcher(
            new ThrowingBackgroundJobClient(), NullLogger<HangfireUploadNotificationDispatcher>.Instance);

        var exception = Record.Exception(() =>
            dispatcher.Enqueue(new UploadNotificationDto { CompanyId = 7 }));

        Assert.Null(exception);
    }

    private sealed class RecordingBackgroundJobClient : IBackgroundJobClient
    {
        public List<Job> Created { get; } = [];

        public string Create(Job job, IState state)
        {
            Created.Add(job);
            return Guid.NewGuid().ToString();
        }

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    private sealed class ThrowingBackgroundJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) =>
            throw new InvalidOperationException("The Hangfire storage is unavailable.");

        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }
}
