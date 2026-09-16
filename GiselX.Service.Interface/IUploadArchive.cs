using GiselX.Service.Dto;

namespace GiselX.Service.Interface;

/// <summary>
/// Keeps the workbook a distributor actually sent. Until this existed only the parsed rows and a
/// SHA-256 survived an upload, so a disagreement about what was in the file could not be settled by
/// looking at the file.
/// </summary>
public interface IUploadArchive
{
    bool IsConfigured { get; }

    /// <summary>
    /// Stores the bytes and returns the path to record against the batch, or null when archiving is
    /// switched off or the write failed. Never throws: an upload that is already valid must not be
    /// refused because a folder is full or unreachable.
    /// </summary>
    Task<string?> StoreAsync(UploadNotificationKind kind, long reference, string originalFileName,
        byte[] content, CancellationToken cancellationToken);

    /// <summary>Opens a stored file, or null when it is missing — a folder can be pruned or restored.</summary>
    Task<Stream?> OpenAsync(string storedPath, CancellationToken cancellationToken);
}
