namespace GiselX.Service;

public sealed class UploadArchiveOptions
{
    public const string SectionName = "UploadArchive";

    /// <summary>
    /// Folder the uploaded workbooks are kept in. Blank switches archiving off, and uploads carry on
    /// without it rather than failing.
    ///
    /// Point this OUTSIDE the deployment folder. A copy-paste deployment replaces that folder, and
    /// the archive would go with it — the same way appsettings.json was lost.
    /// </summary>
    public string Path { get; set; } = string.Empty;
}
