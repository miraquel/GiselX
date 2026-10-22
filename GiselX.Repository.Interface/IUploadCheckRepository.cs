namespace GiselX.Repository.Interface;

/// <summary>
/// What a company uploaded inside a window. <see cref="MissingStockAreas"/> names the company's active
/// Stock areas with no Stock row in the window; it is empty for a company without areas.
/// </summary>
public sealed record UploadPresence(bool HasSellout, bool HasStock, IReadOnlyList<string> MissingStockAreas);

public interface IUploadCheckRepository
{
    /// <summary>
    /// The window is in UTC because SalesTransaction and Stock are stamped with SYSUTCDATETIME().
    /// </summary>
    Task<UploadPresence> GetUploadPresenceAsync(int companyId, DateTime fromUtc, DateTime toUtc,
        CancellationToken cancellationToken);
}
