namespace GiselX.Service;

/// <summary>
/// Ties the workbook a user reviewed to the workbook that is published. Preflight reports the SHA-256 of
/// the bytes it analysed and the upload must quote it back, so confirming a preview of one file and then
/// sending another is refused rather than silently accepted — the totals a user approved would otherwise
/// describe a file that never reached the database.
/// </summary>
internal static class UploadConfirmation
{
    internal const string NotConfirmed =
        "Periksa file di preview dan konfirmasi sebelum upload.";

    internal const string DifferentWorkbook =
        "File yang dikonfirmasi berbeda dengan file yang diupload. "
        + "Periksa file baru di preview sebelum mengupload.";

    /// <summary>Why this upload may not proceed, or null when the confirmed workbook is the one sent.</summary>
    internal static string? DescribeMismatch(string? confirmedSha256, string uploadedSha256) =>
        string.IsNullOrWhiteSpace(confirmedSha256)
            ? NotConfirmed
            : string.Equals(confirmedSha256.Trim(), uploadedSha256, StringComparison.OrdinalIgnoreCase)
                ? null
                : DifferentWorkbook;
}
