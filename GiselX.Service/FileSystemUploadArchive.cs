using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GiselX.Service;

/// <summary>
/// Stores uploaded workbooks under a configured folder.
///
/// The stored name is built from the batch id and a whitelisted extension, never from the name the
/// distributor sent. That name is attacker-controlled — "..\..\web.config" is a perfectly ordinary
/// string in a multipart upload — and keeping it out of the path removes the traversal question
/// rather than trying to sanitise it. The original name is kept in the database and used only as the
/// download's suggested filename.
/// </summary>
public sealed class FileSystemUploadArchive(
    IOptions<UploadArchiveOptions> options,
    ILogger<FileSystemUploadArchive> logger) : IUploadArchive
{
    private static readonly string[] AllowedExtensions = [".xlsx", ".xls"];

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.Value.Path);

    public async Task<string?> StoreAsync(UploadNotificationKind kind, long reference,
        string originalFileName, byte[] content, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return null;

        try
        {
            var relative = BuildRelativePath(kind, reference, originalFileName);
            var absolute = Path.Combine(Root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            await File.WriteAllBytesAsync(absolute, content, cancellationToken);
            return relative;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or NotSupportedException or ArgumentException)
        {
            // The rows are already valid and about to be published. Losing the archive copy is worth
            // a loud log, not a failed upload the distributor has to repeat.
            logger.LogError(exception,
                "Could not archive the {Kind} upload for reference {Reference}. The data was still " +
                "accepted; only the source file was not kept.", kind, reference);
            return null;
        }
    }

    public Task<Stream?> OpenAsync(string storedPath, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return Task.FromResult<Stream?>(null);

        // Re-checked on the way out as well: the value comes from the database, but a stored path is
        // still resolved against the root and refused if it escapes it.
        var absolute = Path.GetFullPath(Path.Combine(Root, storedPath));
        var root = Path.GetFullPath(Root);
        if (!absolute.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("Refused to open '{Path}': it resolves outside the upload archive.", storedPath);
            return Task.FromResult<Stream?>(null);
        }

        if (!File.Exists(absolute))
        {
            logger.LogWarning("Archived upload '{Path}' is no longer on disk.", storedPath);
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(File.OpenRead(absolute));
    }

    /// <summary>
    /// Proves the round trip rather than reporting on the configuration: the folder can exist and
    /// still be read-only to the app pool identity, and a path typed with a stray space resolves to
    /// somewhere nobody will ever look. Writes under a probe folder so a failed run leaves litter
    /// that is obviously not an upload, and removes what it wrote either way.
    /// </summary>
    public async Task<UploadArchiveCheck> CheckAsync(CancellationToken cancellationToken)
    {
        if (!IsConfigured) return UploadArchiveCheck.NotConfigured();

        string? resolved = null;
        string? probe = null;
        try
        {
            resolved = Path.GetFullPath(Root);
            var folder = Path.Combine(resolved, ProbeFolder);
            Directory.CreateDirectory(folder);

            probe = Path.Combine(folder, $"{Guid.NewGuid():N}.probe");
            var written = Guid.NewGuid().ToByteArray();
            await File.WriteAllBytesAsync(probe, written, cancellationToken);
            var read = await File.ReadAllBytesAsync(probe, cancellationToken);

            return read.SequenceEqual(written)
                ? new UploadArchiveCheck { IsConfigured = true, ResolvedPath = resolved, CanStore = true }
                : new UploadArchiveCheck
                {
                    IsConfigured = true,
                    ResolvedPath = resolved,
                    Error = "The probe file read back different bytes than were written to it."
                };
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                              or NotSupportedException or ArgumentException
                                              or PathTooLongException)
        {
            logger.LogError(exception, "The upload archive at '{Path}' failed its write check.", Root);
            return new UploadArchiveCheck
            {
                IsConfigured = true,
                ResolvedPath = resolved,
                Error = exception.Message
            };
        }
        finally
        {
            TryDelete(probe);
        }
    }

    private void TryDelete(string? probe)
    {
        if (probe is null) return;

        try
        {
            File.Delete(probe);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The check has already answered the question it was asked; a probe left behind is
            // untidy, not a failure, and must not turn a successful result into an error.
            logger.LogWarning(exception, "Could not remove the archive probe file '{Path}'.", probe);
        }
    }

    private const string ProbeFolder = "_selftest";

    private string Root => options.Value.Path;

    /// <summary>
    /// Grouped by kind and month so a year of uploads does not land in one directory, and named by
    /// the batch id so two distributors sending "stock.xlsx" on the same day cannot collide.
    /// </summary>
    private static string BuildRelativePath(UploadNotificationKind kind, long reference, string originalFileName)
    {
        var extension = Path.GetExtension(originalFileName);
        if (!AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase)) extension = ".xlsx";

        var today = DateTime.UtcNow;
        return Path.Combine(kind.ToString(), today.ToString("yyyy"), today.ToString("MM"),
            $"{reference}{extension.ToLowerInvariant()}");
    }
}
