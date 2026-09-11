using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

public sealed class StockUploadServiceTests
{
    [Fact]
    public async Task Preflight_performs_no_writes()
    {
        var (service, repository, _) = CreateService(Analysis(1, 0));

        var response = await service.PreflightAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7, CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Empty(repository.Batches);
        Assert.Empty(repository.StagingRows);
    }

    [Fact]
    public async Task Preflight_summarizes_stock_values_from_valid_rows_only()
    {
        var analysis = Analysis(2, 1);
        analysis.Rows[0].SaldoAwal = 10m;
        analysis.Rows[0].SaldoMasukPO = 2m;
        analysis.Rows[0].SaldoAkhir = 12m;
        analysis.Rows[1].SaldoAwal = 20m;
        analysis.Rows[1].SaldoMasukPO = 3m;
        analysis.Rows[1].SaldoAkhir = 23m;
        analysis.Rows[2].SaldoAwal = 999m;
        analysis.Rows[2].SaldoMasukPO = 999m;
        analysis.Rows[2].SaldoAkhir = 999m;
        var (service, _, _) = CreateService(analysis);

        var response = await service.PreflightAsync(
            File(), "stock.xlsx", new DateTime(2026, 8, 10), 7, CancellationToken.None);

        Assert.Equal(30m, response.Data!.TotalSaldoAwal);
        Assert.Equal(5m, response.Data.TotalSaldoMasukPo);
        Assert.Equal(35m, response.Data.TotalSaldoAkhir);
    }

    [Fact]
    public async Task Upload_requires_explicit_confirmation()
    {
        var (service, repository, transaction) = CreateService(Analysis(1, 0));

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            false, "user", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Empty(repository.Batches);
        Assert.Equal(0, transaction.CommitCount);
    }

    [Fact]
    public async Task Upload_stages_every_row_and_publishes_only_valid_rows_with_warning_status()
    {
        var (service, repository, transaction) = CreateService(Analysis(2, 1));

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "user", CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(3, repository.StagingRows.Count);
        Assert.Equal(2, repository.PublishedSourceRows.Count);
        Assert.Equal(StockUploadBatchStatus.CompletedWithWarnings, response.Data!.Status);
        Assert.Equal((3, 2, 1), (response.Data.TotalRows, response.Data.ValidRows, response.Data.InvalidRows));
        Assert.Equal(1, transaction.CommitCount);
    }

    [Fact]
    public async Task Upload_with_zero_valid_rows_fails_but_commits_staging_diagnostics()
    {
        var (service, repository, transaction) = CreateService(Analysis(0, 2));

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "user", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal(2, repository.StagingRows.Count);
        Assert.Empty(repository.PublishedSourceRows);
        Assert.Equal(StockUploadBatchStatus.Failed, repository.Batches.Single().Status);
        Assert.Equal(1, transaction.CommitCount);
    }

    [Fact]
    public async Task Upload_copies_price_unit_to_staging_before_publication()
    {
        var analysis = Analysis(1, 0);
        analysis.Rows[0].PriceUnit = 12.50m;
        var (service, repository, _) = CreateService(analysis);

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "user", CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(12.50m, Assert.Single(repository.StagingRows).PriceUnit);
        Assert.Equal([3], repository.PublishedSourceRows);
    }

    [Fact]
    public async Task Upload_copies_smallest_unit_fields_to_staging_before_publication()
    {
        var analysis = Analysis(1, 0);
        analysis.Rows[0].SmallestUnit = "PCS";
        analysis.Rows[0].SmallestUnitQty = 24m;
        var (service, repository, _) = CreateService(analysis);

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "user", CancellationToken.None);

        Assert.True(response.IsSuccess);
        var staged = Assert.Single(repository.StagingRows);
        Assert.Equal("PCS", staged.SmallestUnit);
        Assert.Equal(24m, staged.SmallestUnitQty);
        Assert.Equal([3], repository.PublishedSourceRows);
    }

    [Fact]
    public async Task Upload_forwards_both_item_identities_and_mapping_id_to_stock_staging()
    {
        var analysis = Analysis(1, 0);
        analysis.Rows[0].DistributorProductId = "P-DIST";
        analysis.Rows[0].DistributorProductName = "Distributor Product";
        analysis.Rows[0].ItemMappingId = 9;
        analysis.Rows[0].ProductId = "4100039";
        analysis.Rows[0].ProductName = "GMK Product";
        var (service, repository, _) = CreateService(analysis);

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "user", CancellationToken.None);

        Assert.True(response.IsSuccess);
        var staging = Assert.Single(repository.StagingRows);
        Assert.Equal("4100039", staging.ProductId);
        Assert.Equal("GMK Product", staging.ProductName);
        Assert.Equal("P-DIST", staging.DistributorProductId);
        Assert.Equal("Distributor Product", staging.DistributorProductName);
        Assert.Equal(9, staging.ItemMappingId);
    }

    [Fact]
    public async Task Upload_records_the_worksheet_and_mapping_each_staged_row_came_from()
    {
        var (service, repository, _) = CreateService(MultiSheetAnalysis());

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "user", CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal([("Stok Gudang", 11), ("Stok Transit", 12)],
            repository.StagingRows.Select(row => (row.SourceWorksheetName, row.StockTemplateMappingId)));
    }

    [Fact]
    public async Task Upload_leaves_the_batch_mapping_empty_when_the_workbook_spans_several_mappings()
    {
        var (service, repository, _) = CreateService(MultiSheetAnalysis());

        await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7, true, "user",
            CancellationToken.None);

        Assert.Null(repository.Batches.Single().StockTemplateMappingId);
    }

    [Fact]
    public async Task Upload_still_records_the_batch_mapping_for_a_single_worksheet_workbook()
    {
        var (service, repository, _) = CreateService(Analysis(1, 0));

        await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7, true, "user",
            CancellationToken.None);

        Assert.Equal(5, repository.Batches.Single().StockTemplateMappingId);
    }

    [Fact]
    public async Task Preflight_reports_every_matched_worksheet_and_the_skipped_ones()
    {
        var (service, _, _) = CreateService(MultiSheetAnalysis());

        var response = await service.PreflightAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(["Stok Gudang", "Stok Transit"],
            response.Data!.Sheets.Select(sheet => sheet.WorksheetName));
        Assert.Equal(["Cover"], response.Data.IgnoredWorksheetNames);
        Assert.Equal("Gudang (Stok Gudang), Transit (Stok Transit)", response.Data.MappingName);
    }

    private static (StockUploadService Service, Repository Repository, Transaction Transaction) CreateService(StockWorkbookAnalysisDto analysis)
    {
        var repository = new Repository();
        var transaction = new Transaction();
        return (new StockUploadService(repository, new AnalysisService(analysis), transaction,
            Options.Create(new StockUploadOptions())), repository, transaction);
    }

    private static StockWorkbookAnalysisDto Analysis(int valid, int invalid)
    {
        var rows = Enumerable.Range(1, valid).Select(index => new StockStagingRowDto
        {
            SourceRowNumber = index + 2, ProductId = $"P{index}", ProductName = "Valid", SaldoAkhir = 1, IsValid = true
        }).Concat(Enumerable.Range(1, invalid).Select(index => new StockStagingRowDto
        {
            SourceRowNumber = valid + index + 2, ProductId = $"N{index}", ProductName = "Invalid",
            SaldoAkhir = -1, IsValid = false, ValidationErrorsJson = "[{}]"
        })).ToList();
        return new StockWorkbookAnalysisDto
        {
            Mapping = new StockTemplateMappingDto { Id = 5, Name = "Stock" },
            Rows = rows,
            RowErrors = invalid == 0 ? [] : [new StockStagingErrorDto { SourceRowNumber = valid + 3, Reason = "negative" }]
        };
    }

    /// <summary>
    /// One workbook contributing two mapped worksheets plus one worksheet nothing maps.
    /// </summary>
    private static StockWorkbookAnalysisDto MultiSheetAnalysis() => new()
    {
        Mapping = null,
        Sheets =
        [
            new StockSheetAnalysisDto
            {
                WorksheetName = "Stok Gudang", MappingId = 11, MappingName = "Gudang",
                TotalRows = 1, ValidRows = 1
            },
            new StockSheetAnalysisDto
            {
                WorksheetName = "Stok Transit", MappingId = 12, MappingName = "Transit",
                TotalRows = 1, ValidRows = 1
            }
        ],
        IgnoredWorksheetNames = ["Cover"],
        Rows =
        [
            new StockStagingRowDto
            {
                SourceRowNumber = 3, ProductId = "P1", ProductName = "Gudang row", SaldoAkhir = 1, IsValid = true,
                StockTemplateMappingId = 11, SourceWorksheetName = "Stok Gudang"
            },
            new StockStagingRowDto
            {
                SourceRowNumber = 3, ProductId = "P2", ProductName = "Transit row", SaldoAkhir = 2, IsValid = true,
                StockTemplateMappingId = 12, SourceWorksheetName = "Stok Transit"
            }
        ]
    };

    private static MemoryStream File() => new([1, 2, 3]);

    private sealed class AnalysisService(StockWorkbookAnalysisDto analysis) : IStockWorkbookAnalysisService
    {
        public Task<StockWorkbookAnalysisDto> AnalyzeAsync(byte[] content, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(analysis);
    }

    private sealed class Repository : IStockUploadRepository
    {
        public List<StockUploadBatch> Batches { get; } = [];
        public List<StockStaging> StagingRows { get; } = [];
        public List<int> PublishedSourceRows { get; } = [];
        public Task<long> InsertBatchAsync(StockUploadBatch batch, CancellationToken cancellationToken)
        {
            batch.Id = 9; Batches.Add(batch); return Task.FromResult(9L);
        }
        public Task InsertStagingAsync(IReadOnlyCollection<StockStaging> rows, CancellationToken cancellationToken)
        { StagingRows.AddRange(rows); return Task.CompletedTask; }
        public Task MarkFailedAsync(long batchId, string errorSummary, CancellationToken cancellationToken)
        { Batches.Single().Status = StockUploadBatchStatus.Failed; return Task.CompletedTask; }
        public Task PublishValidRowsAsync(long batchId, int companyId, StockUploadBatchStatus status, CancellationToken cancellationToken)
        { PublishedSourceRows.AddRange(StagingRows.Where(row => row.IsValid).Select(row => row.SourceRowNumber)); Batches.Single().Status = status; return Task.CompletedTask; }
        public Task<(StockUploadBatch Batch, IReadOnlyList<StockStaging> Rows)?> GetBatchAsync(long batchId, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<(StockUploadBatch, IReadOnlyList<StockStaging>)?>(null);
    }

    private sealed class Transaction : IDbTransaction
    {
        public int CommitCount { get; private set; }
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() => CommitCount++;
        public void Dispose() { }
        public void Rollback() { }
    }
}
