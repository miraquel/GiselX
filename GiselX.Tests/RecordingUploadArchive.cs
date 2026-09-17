using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Tests;

/// <summary>Keeps what an upload would have archived, in memory, instead of touching a disk.</summary>
public sealed class RecordingUploadArchive(bool configured = true) : IUploadArchive
{
    private readonly Dictionary<string, byte[]> _files = [];

    public bool IsConfigured => configured;

    public List<string> StoredPaths { get; } = [];

    public Task<string?> StoreAsync(UploadNotificationKind kind, long reference, string originalFileName,
        byte[] content, CancellationToken cancellationToken)
    {
        if (!configured) return Task.FromResult<string?>(null);

        var path = $"{kind}/{reference}{Path.GetExtension(originalFileName)}";
        _files[path] = content;
        StoredPaths.Add(path);
        return Task.FromResult<string?>(path);
    }

    public Task<Stream?> OpenAsync(string storedPath, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(_files.TryGetValue(storedPath, out var bytes)
            ? new MemoryStream(bytes)
            : null);

    public Task<UploadArchiveCheck> CheckAsync(CancellationToken cancellationToken) =>
        Task.FromResult(configured
            ? new UploadArchiveCheck { IsConfigured = true, ResolvedPath = "(in memory)", CanStore = true }
            : UploadArchiveCheck.NotConfigured());
}

/// <summary>Stands in for an archive whose folder is unreachable — the write fails, silently.</summary>
public sealed class FailingUploadArchive : IUploadArchive
{
    public bool IsConfigured => true;

    public Task<string?> StoreAsync(UploadNotificationKind kind, long reference, string originalFileName,
        byte[] content, CancellationToken cancellationToken) => Task.FromResult<string?>(null);

    public Task<Stream?> OpenAsync(string storedPath, CancellationToken cancellationToken) =>
        Task.FromResult<Stream?>(null);

    public Task<UploadArchiveCheck> CheckAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new UploadArchiveCheck
        {
            IsConfigured = true,
            ResolvedPath = @"D:\unreachable",
            Error = "Access to the path is denied."
        });
}
