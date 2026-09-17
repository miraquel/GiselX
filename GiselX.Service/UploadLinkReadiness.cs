using GiselX.Repository.Interface;
using GiselX.Service.Dto;

namespace GiselX.Service;

/// <summary>One thing that has to be true before a notification can carry a link.</summary>
public sealed record UploadLinkRequirement(string Name, bool IsMet, string Detail, string? Action);

/// <summary>
/// What a notification sent right now for one pipeline would actually contain, with the links spelled
/// out so they can be clicked from the screen rather than fished out of a delivered mail.
/// </summary>
public sealed record UploadLinkSample
{
    public UploadNotificationKind Kind { get; init; }
    public long? Reference { get; init; }
    public string? OriginalFileName { get; init; }
    public bool Archived { get; init; }
    public bool FileOnDisk { get; init; }

    /// <summary>
    /// True when the upload belongs to a different distributor than the person reading the screen.
    /// The links still work — for that distributor. Both pages are scoped to the reader's own
    /// company, so an Administrator clicking them here is turned away, and without saying so the
    /// screen would invite exactly that and look broken.
    /// </summary>
    public bool BelongsToAnotherCompany { get; init; }
    public string? ResultLink { get; init; }
    public string? DownloadLink { get; init; }
    public string Diagnosis { get; init; } = string.Empty;
}

/// <summary>The latest published upload of one kind, and whether its archived file is still there.</summary>
public sealed record LatestUploadState(UploadNotificationKind Kind, LatestUploadSummary? Summary, bool FileOnDisk);

/// <summary>
/// Answers "will the next upload notification carry its links, and if not, which of the two
/// requirements is missing?" — without waiting for an upload to find out.
///
/// The download link needs three things to line up: a dashboard address, a batch to point at, and a
/// workbook that was actually kept. All three are configuration or history rather than code, each
/// fails silently on its own, and the mail omits the line instead of complaining. So an absent link
/// looks identical to a fault, and the only way to tell them apart used to be reading the source.
///
/// This deliberately reuses <see cref="UploadNotificationMessage"/> to build the URLs. A screen that
/// composed its own would eventually disagree with the mail, and a check that disagrees with the
/// thing it checks is worse than no check.
/// </summary>
public static class UploadLinkReadiness
{
    /// <param name="viewerCompanyId">
    /// The company of whoever is looking. An Administrator's samples are drawn from every company,
    /// while the result and download pages are scoped to the reader's own — so the report has to say
    /// which links the reader themselves can open.
    /// </param>
    public static UploadLinkReadinessReport Build(EmailSettings settings, UploadArchiveCheck archive,
        IReadOnlyList<LatestUploadState> latest, int? viewerCompanyId = null)
    {
        var dashboard = settings.DashboardBaseUrl?.Trim();
        var hasDashboard = !string.IsNullOrWhiteSpace(dashboard);

        var requirements = new List<UploadLinkRequirement>
        {
            new("Dashboard address",
                hasDashboard,
                hasDashboard
                    ? dashboard!
                    : "Not set, so no notification carries any link at all.",
                hasDashboard
                    ? null
                    : "Set 'Dashboard Base URL' on the Email Settings screen, e.g. https://giselx-dev.gandummas.co.id"),
            BuildArchiveRequirement(archive)
        };

        return new UploadLinkReadinessReport
        {
            Requirements = requirements,
            Samples = [.. latest.Select(state => BuildSample(state, settings, hasDashboard, viewerCompanyId))]
        };
    }

    private static UploadLinkRequirement BuildArchiveRequirement(UploadArchiveCheck archive)
    {
        if (!archive.IsConfigured)
        {
            return new UploadLinkRequirement("Upload archive folder", false,
                "Not configured, so uploaded workbooks are discarded after they are parsed and no " +
                "download link can ever be offered.",
                "Set UploadArchive__Path to a folder OUTSIDE the deployment folder and restart the " +
                "application — a deployment replaces the deployment folder and would take the archive with it.");
        }

        if (!archive.CanStore)
        {
            return new UploadLinkRequirement("Upload archive folder", false,
                $"'{archive.ResolvedPath}' could not be written to and read back: {archive.Error}",
                "Grant the application pool identity modify rights on that folder, or point " +
                "UploadArchive__Path somewhere it has them. Uploads will still succeed — only the " +
                "workbook is lost, and silently.");
        }

        return new UploadLinkRequirement("Upload archive folder", true,
            $"'{archive.ResolvedPath}' — a probe file was written, read back and removed just now.", null);
    }

    private static UploadLinkSample BuildSample(LatestUploadState state, EmailSettings settings, bool hasDashboard,
        int? viewerCompanyId)
    {
        var kind = state.Kind;
        if (state.Summary is not { } summary)
        {
            return new UploadLinkSample
            {
                Kind = kind,
                Diagnosis = $"No completed {kind} upload has been found, so a sample has nothing real " +
                            "to point at and is sent with illustrative figures and no links."
            };
        }

        var archived = !string.IsNullOrWhiteSpace(summary.ArchivedFilePath);
        var notification = new UploadNotificationDto
        {
            Kind = kind,
            Reference = summary.Reference,
            SourceFileArchived = archived
        };

        var resultLink = UploadNotificationMessage.BuildResultLink(notification, settings);
        var downloadLink = UploadNotificationMessage.BuildSourceFileLink(notification, settings);
        var elsewhere = viewerCompanyId is not null && summary.CompanyId != viewerCompanyId;

        return new UploadLinkSample
        {
            Kind = kind,
            Reference = summary.Reference,
            OriginalFileName = summary.OriginalFileName,
            Archived = archived,
            FileOnDisk = state.FileOnDisk,
            BelongsToAnotherCompany = elsewhere,
            ResultLink = resultLink,
            DownloadLink = downloadLink,
            Diagnosis = Describe(kind, summary.Reference, archived, state.FileOnDisk, hasDashboard)
                        + (elsewhere
                            ? " It belongs to another distributor: both pages are scoped to the " +
                              "reader's own company, so these links open for them and not for you."
                            : string.Empty)
        };
    }

    private static string Describe(UploadNotificationKind kind, long reference, bool archived, bool fileOnDisk,
        bool hasDashboard) => (hasDashboard, archived, fileOnDisk) switch
    {
        (false, _, _) =>
            $"{kind} upload #{reference} was found, but no link can be built until the dashboard " +
            "address is set.",
        (true, false, _) =>
            $"{kind} upload #{reference} was made before the archive was configured, so its workbook " +
            "was never kept. The result link works; the download link cannot be offered. Uploads made " +
            "from now on will carry both.",
        (true, true, false) =>
            $"{kind} upload #{reference} recorded an archived workbook, but it is no longer in the " +
            "folder. The mail offers the download link and it will report the file as unavailable.",
        (true, true, true) =>
            $"{kind} upload #{reference} was archived and the file is still there — both links below " +
            "are live, and a notification sent now carries them."
    };
}

/// <summary>Everything the Email Test screen needs to say why a link is or is not there.</summary>
public sealed record UploadLinkReadinessReport
{
    public IReadOnlyList<UploadLinkRequirement> Requirements { get; init; } = [];

    public IReadOnlyList<UploadLinkSample> Samples { get; init; } = [];

    /// <summary>True when a download link is possible at all — every upload from now on will carry one.</summary>
    public bool DownloadLinksPossible => Requirements.All(requirement => requirement.IsMet);
}
