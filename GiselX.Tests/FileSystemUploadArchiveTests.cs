using System.Text;
using GiselX.Service;
using GiselX.Service.Dto;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

public sealed class FileSystemUploadArchiveTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "giselx-archive-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task A_workbook_is_stored_and_read_back_byte_for_byte()
    {
        var archive = Create();
        var content = Encoding.UTF8.GetBytes("workbook bytes");

        var path = await archive.StoreAsync(UploadNotificationKind.Stock, 901, "stock.xlsx",
            content, CancellationToken.None);

        Assert.NotNull(path);
        await using var stream = await archive.OpenAsync(path!, CancellationToken.None);
        using var reader = new StreamReader(stream!);
        Assert.Equal("workbook bytes", await reader.ReadToEndAsync());
    }

    [Theory]
    [InlineData(@"..\..\web.config")]
    [InlineData("../../appsettings.json")]
    [InlineData(@"C:\Windows\System32\drivers\etc\hosts")]
    [InlineData("nested/path/stock.xlsx")]
    public async Task The_uploaded_name_never_shapes_the_path(string hostileName)
    {
        // The name arrives from a multipart upload and is entirely attacker-controlled. The stored
        // path is built from the batch id, so none of these can escape the archive folder.
        var archive = Create();

        var path = await archive.StoreAsync(UploadNotificationKind.Stock, 77, hostileName,
            [1, 2, 3], CancellationToken.None);

        Assert.NotNull(path);
        var absolute = Path.GetFullPath(Path.Combine(_root, path!));
        Assert.StartsWith(Path.GetFullPath(_root) + Path.DirectorySeparatorChar, absolute,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("77.xlsx", Path.GetFileName(absolute));
    }

    [Fact]
    public async Task A_stored_path_that_escapes_the_root_is_refused_on_the_way_out()
    {
        // Defence in depth: the value comes from our own database, but a tampered row must not be
        // able to read web.config back out through the download endpoint.
        var archive = Create();

        Assert.Null(await archive.OpenAsync(@"..\..\appsettings.json", CancellationToken.None));
    }

    [Fact]
    public async Task Two_distributors_sending_the_same_filename_do_not_collide()
    {
        var archive = Create();

        var first = await archive.StoreAsync(UploadNotificationKind.Stock, 1, "stock.xlsx", [1], CancellationToken.None);
        var second = await archive.StoreAsync(UploadNotificationKind.Stock, 2, "stock.xlsx", [2], CancellationToken.None);

        Assert.NotEqual(first, second);
    }

    [Fact]
    public async Task A_legacy_xls_keeps_its_own_extension()
    {
        var archive = Create();

        var path = await archive.StoreAsync(UploadNotificationKind.Sellout, 5, "sellout.XLS",
            [1], CancellationToken.None);

        Assert.EndsWith("5.xls", path);
    }

    [Fact]
    public async Task With_no_folder_configured_nothing_is_stored_and_nothing_throws()
    {
        var archive = new FileSystemUploadArchive(
            Options.Create(new UploadArchiveOptions { Path = "" }),
            NullLogger<FileSystemUploadArchive>.Instance);

        Assert.False(archive.IsConfigured);
        Assert.Null(await archive.StoreAsync(UploadNotificationKind.Stock, 1, "a.xlsx", [1], CancellationToken.None));
        Assert.Null(await archive.OpenAsync("Stock/1.xlsx", CancellationToken.None));
    }

    [Fact]
    public async Task A_missing_file_reads_as_absent_rather_than_throwing()
    {
        // Folders get pruned and restored; the download should say "not available", not crash.
        Assert.Null(await Create().OpenAsync("Stock/2026/09/999.xlsx", CancellationToken.None));
    }

    private FileSystemUploadArchive Create() => new(
        Options.Create(new UploadArchiveOptions { Path = _root }),
        NullLogger<FileSystemUploadArchive>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
