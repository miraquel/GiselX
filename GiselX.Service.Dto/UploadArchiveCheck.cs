namespace GiselX.Service.Dto;

/// <summary>
/// The result of asking the archive to prove it works, rather than assuming it does.
///
/// Archiving is deliberately silent at upload time — a folder that is missing or read-only must not
/// fail an upload whose rows are already valid — which means a misconfigured path is invisible until
/// somebody notices months later that no notification ever carried a download link. This is how that
/// is found out on purpose instead.
/// </summary>
public sealed record UploadArchiveCheck
{
    /// <summary>False when no folder is configured at all, which switches archiving off entirely.</summary>
    public bool IsConfigured { get; init; }

    /// <summary>The folder as the process resolved it — a relative path is worth seeing spelled out.</summary>
    public string? ResolvedPath { get; init; }

    /// <summary>True only when a file was actually written, read back byte-for-byte and removed again.</summary>
    public bool CanStore { get; init; }

    /// <summary>Why the round trip failed, in the words the framework used. Null when it succeeded.</summary>
    public string? Error { get; init; }

    public static UploadArchiveCheck NotConfigured() => new();
}
