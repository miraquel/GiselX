using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

/// <summary>
/// The download link depends on three things lining up, each of which fails silently on its own, and
/// the mail omits the line rather than complaining. That made "there is no link" and "something is
/// broken" indistinguishable without reading the source. These pin the answer the Email Test screen
/// gives instead.
/// </summary>
public sealed class UploadLinkReadinessTests
{
    [Fact]
    public void Everything_configured_and_archived_offers_both_links()
    {
        var report = Build(Settings(), Working(), Archived());

        Assert.True(report.DownloadLinksPossible);
        var sample = Assert.Single(report.Samples);
        Assert.Equal("https://giselx-dev.gandummas.co.id/Stocks/Result?batchId=128", sample.ResultLink);
        Assert.Equal("https://giselx-dev.gandummas.co.id/Stocks/DownloadSource?batchId=128",
            sample.DownloadLink);
    }

    [Fact]
    public void The_links_shown_are_the_ones_the_mail_actually_sends()
    {
        // A screen that composed its own copy of these URLs could report a link that works while the
        // mail sends one that does not — a check that disagrees with the thing it checks.
        var settings = Settings();
        var sample = Assert.Single(Build(settings, Working(), Archived()).Samples);

        var rendered = UploadNotificationMessage.ForDistributor(new UploadNotificationDto
        {
            Kind = UploadNotificationKind.Stock,
            Reference = 128,
            SourceFileArchived = true,
            TotalRows = 1,
            ValidRows = 1
        }, "Cahaya Setia Utama", settings);

        Assert.Contains(sample.ResultLink!, rendered.Body, StringComparison.Ordinal);
        Assert.Contains(sample.DownloadLink!, rendered.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_a_dashboard_address_nothing_links_at_all()
    {
        var settings = Settings();
        settings.DashboardBaseUrl = "";

        var report = Build(settings, Working(), Archived());

        Assert.False(report.DownloadLinksPossible);
        Assert.False(report.Requirements[0].IsMet);
        var sample = Assert.Single(report.Samples);
        Assert.Null(sample.ResultLink);
        Assert.Null(sample.DownloadLink);
        Assert.Contains("dashboard address", sample.Diagnosis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void An_unconfigured_archive_is_told_apart_from_a_broken_one()
    {
        // The remedies differ — set the variable, versus fix the rights on the folder — so the two
        // must not collapse into one "archive unavailable" message.
        var off = Build(Settings(), UploadArchiveCheck.NotConfigured(), Archived()).Requirements[1];
        var broken = Build(Settings(), Broken(), Archived()).Requirements[1];

        Assert.False(off.IsMet);
        Assert.False(broken.IsMet);
        Assert.Contains("UploadArchive__Path", off.Action!, StringComparison.Ordinal);
        Assert.Contains("Access to the path is denied", broken.Detail, StringComparison.Ordinal);
        Assert.NotEqual(off.Detail, broken.Detail);
    }

    [Fact]
    public void The_archive_advice_says_to_keep_the_folder_out_of_the_deployment()
    {
        // A deployment replaces the deployment folder, which is how appsettings.json was lost once
        // already. An archive inside it would be deleted along with every workbook it held.
        var requirement = Build(Settings(), UploadArchiveCheck.NotConfigured(), Archived()).Requirements[1];

        Assert.Contains("OUTSIDE the deployment folder", requirement.Action!, StringComparison.Ordinal);
    }

    [Fact]
    public void An_upload_made_before_archiving_keeps_its_result_link_and_explains_the_missing_one()
    {
        var report = Build(Settings(), Working(), NotArchived());

        var sample = Assert.Single(report.Samples);
        Assert.NotNull(sample.ResultLink);
        Assert.Null(sample.DownloadLink);
        Assert.False(sample.Archived);
        Assert.Contains("before the archive was configured", sample.Diagnosis, StringComparison.Ordinal);
    }

    [Fact]
    public void A_recorded_file_that_is_no_longer_on_disk_is_called_out()
    {
        // The mail offers the link on the strength of the recorded path alone, so a pruned folder
        // produces a link that fails only when the distributor clicks it.
        var report = Build(Settings(), Working(), Archived(onDisk: false));

        var sample = Assert.Single(report.Samples);
        Assert.NotNull(sample.DownloadLink);
        Assert.False(sample.FileOnDisk);
        Assert.Contains("no longer in the folder", sample.Diagnosis, StringComparison.Ordinal);
    }

    [Fact]
    public void With_no_completed_upload_there_is_nothing_to_link_to()
    {
        var report = Build(Settings(), Working(),
            new LatestUploadState(UploadNotificationKind.Sellout, null, false));

        var sample = Assert.Single(report.Samples);
        Assert.Null(sample.Reference);
        Assert.Null(sample.ResultLink);
        Assert.Null(sample.DownloadLink);
        Assert.Contains("illustrative figures", sample.Diagnosis, StringComparison.Ordinal);
    }

    [Fact]
    public void Readiness_is_about_configuration_not_about_the_last_upload()
    {
        // An upload made yesterday cannot be archived retroactively, but that says nothing about
        // whether the next one will be. The two rows answer "will this work from now on".
        var report = Build(Settings(), Working(), NotArchived());

        Assert.True(report.DownloadLinksPossible);
    }

    [Fact]
    public void Another_distributors_upload_is_flagged_because_the_links_will_not_open_here()
    {
        // An Administrator's sample is drawn from every company while the result and download pages
        // are scoped to the reader's own. Inviting them to click a link that turns them away is how
        // a working feature gets reported as broken.
        var report = UploadLinkReadiness.Build(Settings(), Working(), [Archived()], viewerCompanyId: 99);

        var sample = Assert.Single(report.Samples);
        Assert.True(sample.BelongsToAnotherCompany);
        Assert.Contains("open for them and not for you", sample.Diagnosis, StringComparison.Ordinal);
        Assert.NotNull(sample.DownloadLink);
    }

    [Fact]
    public void Ones_own_upload_carries_no_such_warning()
    {
        var report = UploadLinkReadiness.Build(Settings(), Working(), [Archived()], viewerCompanyId: 7);

        var sample = Assert.Single(report.Samples);
        Assert.False(sample.BelongsToAnotherCompany);
        Assert.DoesNotContain("another distributor", sample.Diagnosis, StringComparison.OrdinalIgnoreCase);
    }

    private static UploadLinkReadinessReport Build(EmailSettings settings, UploadArchiveCheck archive,
        LatestUploadState state) => UploadLinkReadiness.Build(settings, archive, [state]);

    private static EmailSettings Settings() => new()
    {
        DashboardBaseUrl = "https://giselx-dev.gandummas.co.id/",
        FromName = "GiselX System"
    };

    private static UploadArchiveCheck Working() => new()
    {
        IsConfigured = true,
        ResolvedPath = @"D:\GiselXArchive",
        CanStore = true
    };

    private static UploadArchiveCheck Broken() => new()
    {
        IsConfigured = true,
        ResolvedPath = @"D:\GiselXArchive",
        Error = "Access to the path is denied."
    };

    private static LatestUploadState Archived(bool onDisk = true) => new(UploadNotificationKind.Stock,
        new LatestUploadSummary
        {
            Reference = 128,
            CompanyId = 7,
            OriginalFileName = "DATA STOCK GMK.xlsx",
            ArchivedFilePath = @"Stock\2026\09\128.xlsx"
        }, onDisk);

    private static LatestUploadState NotArchived() => new(UploadNotificationKind.Stock,
        new LatestUploadSummary
        {
            Reference = 128,
            OriginalFileName = "DATA STOCK GMK.xlsx",
            ArchivedFilePath = null
        }, false);
}
