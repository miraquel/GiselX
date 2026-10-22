using System.Data;
using System.Text;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

/// <summary>
/// The download link travels in an email, which gets forwarded. What keeps one distributor out of
/// another's file is that the lookup is scoped by the signed-in user's own company — never by
/// anything in the request — so these are the tests that matter most about this feature.
/// </summary>
public sealed class UploadSourceDownloadTests
{
    private const int OwningCompany = 7;
    private const int OtherCompany = 9;

    [Fact]
    public async Task The_owning_company_gets_the_file_under_its_original_name()
    {
        var (service, _) = CreateService(archivedPath: "Stock/901.xlsx");

        var response = await service.DownloadSourceAsync(901, OwningCompany, CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal("stock-anakmas.xlsx", response.Data!.FileName);
        using var reader = new StreamReader(response.Data.Content);
        Assert.Equal("the original workbook", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Another_company_is_refused_even_with_the_right_batch_id()
    {
        var (service, _) = CreateService(archivedPath: "Stock/901.xlsx");

        var response = await service.DownloadSourceAsync(901, OtherCompany, CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task An_upload_made_before_archiving_was_switched_on_says_so()
    {
        var (service, _) = CreateService(archivedPath: null);

        var response = await service.DownloadSourceAsync(901, OwningCompany, CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("tidak diarsipkan"));
    }

    [Fact]
    public async Task A_file_that_has_since_left_the_disk_reports_rather_than_throws()
    {
        var (service, _) = CreateService(archivedPath: "Stock/does-not-exist.xlsx");

        var response = await service.DownloadSourceAsync(901, OwningCompany, CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("sudah tidak tersedia"));
    }

    private static (StockUploadService Service, RecordingUploadArchive Archive) CreateService(string? archivedPath)
    {
        var archive = new RecordingUploadArchive();
        if (archivedPath == "Stock/901.xlsx")
        {
            archive.StoreAsync(UploadNotificationKind.Stock, 901, "stock.xlsx",
                Encoding.UTF8.GetBytes("the original workbook"), CancellationToken.None).GetAwaiter().GetResult();
        }

        var service = new StockUploadService(
            new ScopedRepository(archivedPath),
            new UnusedAnalysisService(),
            new NoopTransaction(),
            Options.Create(new StockUploadOptions()),
            new RecordingUploadNotificationDispatcher(),
            archive,
            new StubStockAreaRepository([]));
        return (service, archive);
    }

    /// <summary>Returns the batch only for the company that owns it, the way the real SQL does.</summary>
    private sealed class ScopedRepository(string? archivedPath) : IStockUploadRepository
    {
        public Task<(StockUploadBatch Batch, IReadOnlyList<StockStaging> Rows)?> GetBatchAsync(
            long batchId, int companyId, CancellationToken cancellationToken)
        {
            if (companyId != OwningCompany)
                return Task.FromResult<(StockUploadBatch, IReadOnlyList<StockStaging>)?>(null);

            var batch = new StockUploadBatch
            {
                Id = batchId,
                CompanyId = OwningCompany,
                OriginalFileName = "stock-anakmas.xlsx",
                ArchivedFilePath = archivedPath
            };
            return Task.FromResult<(StockUploadBatch, IReadOnlyList<StockStaging>)?>((batch, []));
        }

        public Task<long> InsertBatchAsync(StockUploadBatch batch, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task InsertStagingAsync(IReadOnlyCollection<StockStaging> rows, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SetArchivedFilePathAsync(long batchId, string archivedFilePath,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task PublishValidRowsAsync(long batchId, int companyId, StockUploadBatchStatus status,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task MarkFailedAsync(long batchId, string errorSummary, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedAnalysisService : IStockWorkbookAnalysisService
    {
        public Task<StockWorkbookAnalysisDto> AnalyzeAsync(byte[] workbook, int companyId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoopTransaction : IDbTransaction
    {
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() { }
        public void Rollback() { }
        public void Dispose() { }
    }
}
