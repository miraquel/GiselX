using GiselX.Domain;

namespace GiselX.Repository.Interface;

/// <summary>What the Email Test screen needs to render a sample against a real upload.</summary>
public sealed class LatestUploadSummary
{
    public long Reference { get; set; }

    /// <summary>
    /// Who uploaded it. An Administrator's sample looks across companies, but the result and download
    /// pages are scoped to the reader's own company, so this is what says whether the links in the
    /// sample will open for the person looking at them.
    /// </summary>
    public int CompanyId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime? CompletedDate { get; set; }
    public string? ArchivedFilePath { get; set; }

    /// <summary>Snapshot date for Stock; null for Sellout, whose period spans weeks.</summary>
    public DateTime? StockDate { get; set; }
}

public interface ILatestUploadRepository
{
    /// <summary>
    /// The company's most recent published Stock upload, or null when it has never uploaded one.
    /// </summary>
    /// <param name="companyId">
    /// The company to look within, or null for any company. Null is only ever passed for an
    /// Administrator, who already sees across companies everywhere else in this system; a
    /// distributor is always scoped to their own.
    /// </param>
    Task<LatestUploadSummary?> GetLatestStockAsync(int? companyId, CancellationToken cancellationToken);

    Task<LatestUploadSummary?> GetLatestSelloutAsync(int? companyId, CancellationToken cancellationToken);
}
