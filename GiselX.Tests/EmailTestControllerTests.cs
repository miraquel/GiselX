using System.Security.Claims;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

/// <summary>
/// This screen exists so an SMTP or archive change can be checked without performing an upload. That
/// only holds if what it reports is true: a check that overstates what works is worse than none, and
/// the sample's links are the very thing people came here to verify.
/// </summary>
public sealed class EmailTestControllerTests
{
    [Fact]
    public async Task The_page_reports_both_requirements_against_this_server()
    {
        var controller = Create(out _, archiveConfigured: true);

        var model = Assert.IsType<UploadLinkReadinessReport>(
            Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None)).Model);

        Assert.True(model.DownloadLinksPossible);
        Assert.Equal(2, model.Requirements.Count);
        Assert.All(model.Requirements, requirement => Assert.True(requirement.IsMet));
    }

    [Fact]
    public async Task An_archive_that_is_switched_off_makes_the_page_say_so()
    {
        var controller = Create(out _, archiveConfigured: false);

        var model = Assert.IsType<UploadLinkReadinessReport>(
            Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None)).Model);

        Assert.False(model.DownloadLinksPossible);
        Assert.Contains(model.Requirements, requirement => !requirement.IsMet);
    }

    [Fact]
    public async Task The_page_lists_both_pipelines_so_neither_is_checked_by_accident()
    {
        // Stock and Sellout archive independently and one can be configured while the other has
        // never been uploaded, so reporting on only the selected kind would hide half the answer.
        var controller = Create(out _, archiveConfigured: true);

        var model = Assert.IsType<UploadLinkReadinessReport>(
            Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None)).Model);

        Assert.Equal([UploadNotificationKind.Stock, UploadNotificationKind.Sellout],
            model.Samples.Select(sample => sample.Kind));
    }

    [Fact]
    public async Task A_sample_for_an_unarchived_upload_carries_no_download_link_by_default()
    {
        var controller = Create(out var email, archiveConfigured: true, archivedPath: null);

        await controller.SendUploadNotification("a@b.test", "Cahaya Setia Utama",
            UploadNotificationKind.Stock, false, forceDownloadLink: false, CancellationToken.None);

        Assert.False(email.Sent!.SourceFileArchived);
        Assert.Contains("no file-download link", Status(controller), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forcing_the_link_in_never_claims_the_file_is_there()
    {
        // The only way to review the download wording before anything has been archived. The link it
        // produces does not work, and a status message that implied otherwise would turn a review
        // aid into a false pass.
        var controller = Create(out var email, archiveConfigured: true, archivedPath: null);

        await controller.SendUploadNotification("a@b.test", "Cahaya Setia Utama",
            UploadNotificationKind.Stock, false, forceDownloadLink: true, CancellationToken.None);

        Assert.True(email.Sent!.SourceFileArchived);
        var status = Status(controller);
        Assert.Contains("report the file as unavailable", status, StringComparison.Ordinal);
        Assert.Contains("layout", status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_archived_upload_needs_no_forcing_and_is_reported_as_working()
    {
        var controller = Create(out var email, archiveConfigured: true, archivedPath: @"Stock\2026\09\128.xlsx");

        await controller.SendUploadNotification("a@b.test", "Cahaya Setia Utama",
            UploadNotificationKind.Stock, false, forceDownloadLink: false, CancellationToken.None);

        Assert.True(email.Sent!.SourceFileArchived);
        Assert.Contains("working link", Status(controller), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Forcing_the_link_invents_nothing_when_there_is_no_real_upload()
    {
        // Without a batch to point at there is no URL to build, and a fabricated one would 404 for a
        // reason nobody could diagnose. The illustrative sample stays linkless whatever is ticked.
        var controller = Create(out var email, archiveConfigured: true, latest: false);

        await controller.SendUploadNotification("a@b.test", "Cahaya Setia Utama",
            UploadNotificationKind.Stock, false, forceDownloadLink: true, CancellationToken.None);

        Assert.Null(email.Sent!.Reference);
        Assert.False(email.Sent.SourceFileArchived);
        Assert.Contains("carries no links", Status(controller), StringComparison.Ordinal);
    }

    private static string Status(Controller controller) => controller.TempData["StatusMessage"]?.ToString() ?? "";

    private static EmailTestController Create(out CapturingEmailService email, bool archiveConfigured,
        string? archivedPath = null, bool latest = true)
    {
        email = new CapturingEmailService();
        var user = new AppIdentityUser { Id = "u1", UserName = "csu.dist", CompanyId = 3 };

        var controller = new EmailTestController(
            email,
            new StubSettingsProvider(),
            new RecordingUploadArchive(archiveConfigured),
            new StubLatestUploads(latest ? archivedPath ?? string.Empty : null, latest),
            CreateUserManager(user))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, user.Id), new Claim(ClaimTypes.Name, "csu.dist")],
                        "test", ClaimTypes.Name, ClaimTypes.Role))
                }
            },
            TempData = new TempDataDictionary(new DefaultHttpContext(), new NullTempDataProvider())
        };

        return controller;
    }

    private static UserManager<AppIdentityUser> CreateUserManager(AppIdentityUser user) => new(
        new SingleUserStore(user), Options.Create(new IdentityOptions()), new PasswordHasher<AppIdentityUser>(),
        [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
        NullLogger<UserManager<AppIdentityUser>>.Instance);

    private sealed class CapturingEmailService : IEmailService
    {
        public UploadNotificationDto? Sent { get; private set; }

        public Task SendReminderAsync(IReadOnlyCollection<string> recipients, string companyName,
            DateTime deadline, bool isFinalReminder) => Task.CompletedTask;

        public Task SendUploadCompletedAsync(UploadNotificationDto notification, string companyName,
            IReadOnlyCollection<string> distributorRecipients, IReadOnlyCollection<string> internalRecipients,
            CancellationToken cancellationToken)
        {
            Sent = notification;
            return Task.CompletedTask;
        }
    }

    private sealed class StubSettingsProvider : IEmailSettingsProvider
    {
        public Task<EmailSettings> GetAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new EmailSettings
            {
                DashboardBaseUrl = "https://giselx-dev.gandummas.co.id",
                FromName = "GiselX System"
            });
    }

    private sealed class StubLatestUploads(string? archivedPath, bool hasUpload) : ILatestUploadRepository
    {
        public Task<LatestUploadSummary?> GetLatestStockAsync(int? companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Build(128));

        public Task<LatestUploadSummary?> GetLatestSelloutAsync(int? companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Build(77));

        private LatestUploadSummary? Build(long reference) => hasUpload
            ? new LatestUploadSummary
            {
                Reference = reference,
                CompanyId = 3,
                OriginalFileName = "DATA STOCK GMK.xlsx",
                TotalRows = 312,
                ValidRows = 312,
                UploadedBy = "csu.dist",
                CompletedDate = new DateTime(2026, 9, 17, 2, 20, 0, DateTimeKind.Utc),
                ArchivedFilePath = string.IsNullOrEmpty(archivedPath) ? null : archivedPath
            }
            : null;
    }

    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class SingleUserStore(AppIdentityUser user) : IUserStore<AppIdentityUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(AppIdentityUser value, CancellationToken token) =>
            Task.FromResult(value.Id);
        public Task<string?> GetUserNameAsync(AppIdentityUser value, CancellationToken token) =>
            Task.FromResult(value.UserName);
        public Task SetUserNameAsync(AppIdentityUser value, string? name, CancellationToken token) =>
            Task.CompletedTask;
        public Task<string?> GetNormalizedUserNameAsync(AppIdentityUser value, CancellationToken token) =>
            Task.FromResult(value.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(AppIdentityUser value, string? name, CancellationToken token) =>
            Task.CompletedTask;
        public Task<IdentityResult> CreateAsync(AppIdentityUser value, CancellationToken token) =>
            Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(AppIdentityUser value, CancellationToken token) =>
            Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(AppIdentityUser value, CancellationToken token) =>
            Task.FromResult(IdentityResult.Success);
        public Task<AppIdentityUser?> FindByIdAsync(string userId, CancellationToken token) =>
            Task.FromResult<AppIdentityUser?>(userId == user.Id ? user : null);
        public Task<AppIdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken token) =>
            Task.FromResult<AppIdentityUser?>(user);
    }
}
