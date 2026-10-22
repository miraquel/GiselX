using System.Security.Cryptography;
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
    public async Task Preflight_summarizes_cartons_and_pieces_from_valid_rows_only()
    {
        var analysis = Analysis(2, 1);
        analysis.Rows[0].QtyCarton = 10m;
        analysis.Rows[0].QtyPcs = 2m;
        analysis.Rows[1].QtyCarton = 20m;
        analysis.Rows[1].QtyPcs = 3m;
        analysis.Rows[2].QtyCarton = 999m;
        analysis.Rows[2].QtyPcs = 999m;
        var (service, _, _) = CreateService(analysis);

        var response = await service.PreflightAsync(
            File(), "stock.xlsx", new DateTime(2026, 8, 10), 7, CancellationToken.None);

        Assert.Equal(30m, response.Data!.TotalQtyCarton);
        Assert.Equal(5m, response.Data.TotalQtyPcs);
    }

    [Fact]
    public async Task Upload_requires_explicit_confirmation()
    {
        var (service, repository, transaction) = CreateService(Analysis(1, 0));

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            false, "user", ConfirmedSha(), CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Empty(repository.Batches);
        Assert.Equal(0, transaction.CommitCount);
    }

    [Fact]
    public async Task Upload_stages_every_row_and_publishes_only_valid_rows_with_warning_status()
    {
        var (service, repository, transaction) = CreateService(Analysis(2, 1));

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "user", ConfirmedSha(), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(3, repository.StagingRows.Count);
        Assert.Equal(2, repository.PublishedSourceRows.Count);
        Assert.Equal(StockUploadBatchStatus.CompletedWithWarnings, response.Data!.Status);
        Assert.Equal((3, 2, 1), (response.Data.TotalRows, response.Data.ValidRows, response.Data.InvalidRows));
        Assert.Equal(1, transaction.CommitCount);
    }

    [Fact]
    public async Task Upload_stages_the_area_condition_and_raw_texts_of_each_row()
    {
        var analysis = Analysis(1, 0);
        var source = analysis.Rows[0];
        source.StockAreaId = 5;
        source.Condition = StockCondition.Bad;
        source.SourceAreaText = " Medan ";
        source.SourceConditionText = "BS";
        var (service, repository, _) = CreateService(analysis);

        await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "user", ConfirmedSha(), CancellationToken.None);

        var staged = Assert.Single(repository.StagingRows);
        Assert.Equal(5, staged.StockAreaId);
        Assert.Equal(StockCondition.Bad, staged.Condition);
        Assert.Equal(" Medan ", staged.SourceAreaText);
        Assert.Equal("BS", staged.SourceConditionText);
    }

    [Fact]
    public async Task Upload_with_zero_valid_rows_fails_but_commits_staging_diagnostics()
    {
        var (service, repository, transaction) = CreateService(Analysis(0, 2));

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "user", ConfirmedSha(), CancellationToken.None);

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
            true, "user", ConfirmedSha(), CancellationToken.None);

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
            true, "user", ConfirmedSha(), CancellationToken.None);

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
            true, "user", ConfirmedSha(), CancellationToken.None);

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
            true, "user", ConfirmedSha(), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal([("Stok Gudang", 11), ("Stok Transit", 12)],
            repository.StagingRows.Select(row => (row.SourceWorksheetName, row.StockTemplateMappingId)));
    }

    [Fact]
    public async Task Upload_leaves_the_batch_mapping_empty_when_the_workbook_spans_several_mappings()
    {
        var (service, repository, _) = CreateService(MultiSheetAnalysis());

        await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7, true, "user", ConfirmedSha(),
            CancellationToken.None);

        Assert.Null(repository.Batches.Single().StockTemplateMappingId);
    }

    [Fact]
    public async Task Upload_still_records_the_batch_mapping_for_a_single_worksheet_workbook()
    {
        var (service, repository, _) = CreateService(Analysis(1, 0));

        await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7, true, "user", ConfirmedSha(),
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

    [Fact]
    public async Task A_completed_upload_notifies_with_the_batch_totals()
    {
        var notifications = new RecordingUploadNotificationDispatcher();
        var (service, repository, transaction) = CreateService(Analysis(2, 1), notifications);

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "budi", ConfirmedSha(), CancellationToken.None);

        Assert.True(response.IsSuccess);
        var notification = Assert.Single(notifications.Enqueued);
        Assert.Equal(7, notification.CompanyId);
        Assert.Equal(UploadNotificationKind.Stock, notification.Kind);
        Assert.Equal((3, 2, 1), (notification.TotalRows, notification.ValidRows, notification.InvalidRows));
        Assert.Equal("budi", notification.UploadedBy);
        Assert.Equal("stock.xlsx", notification.OriginalFileName);
        Assert.Equal(repository.Batches.Single().Id, notification.Reference);
        Assert.Equal("Snapshot 10 Agustus 2026", notification.PeriodLabel);
        Assert.Equal(1, transaction.CommitCount);
    }

    [Fact]
    public async Task A_refused_upload_notifies_nobody()
    {
        // Every row invalid: the batch is recorded as Failed and nothing is published, so telling
        // anyone their stock arrived would be false.
        var notifications = new RecordingUploadNotificationDispatcher();
        var (service, _, _) = CreateService(Analysis(0, 2), notifications);

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "budi", ConfirmedSha(), CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Empty(notifications.Enqueued);
    }

    [Fact]
    public async Task An_unconfirmed_upload_notifies_nobody()
    {
        var notifications = new RecordingUploadNotificationDispatcher();
        var (service, _, _) = CreateService(Analysis(2, 0), notifications);

        await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            false, "budi", ConfirmedSha(), CancellationToken.None);

        Assert.Empty(notifications.Enqueued);
    }

    private static (StockUploadService Service, Repository Repository, Transaction Transaction) CreateService(
        StockWorkbookAnalysisDto analysis, IUploadNotificationDispatcher? notifications = null,
        IUploadArchive? archive = null, IReadOnlyList<PublishedStockArea>? published = null,
        IReadOnlyList<StockArea>? areas = null)
    {
        var repository = new Repository { PublishedAreas = published ?? [] };
        var transaction = new Transaction();
        return (new StockUploadService(repository, new AnalysisService(analysis), transaction,
            Options.Create(new StockUploadOptions()),
            notifications ?? new RecordingUploadNotificationDispatcher(),
            archive ?? new RecordingUploadArchive(), new StubStockAreaRepository(areas ?? [])), repository, transaction);
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

    [Fact]
    public async Task Upload_refuses_a_workbook_that_is_not_the_one_that_was_confirmed()
    {
        // The preview was reviewed for a different file; publishing this one would record totals nobody saw.
        var (service, repository, transaction) = CreateService(Analysis(1, 0));
        var otherWorkbook = Convert.ToHexString(SHA256.HashData([9, 9, 9])).ToLowerInvariant();

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "user", otherWorkbook, CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains("berbeda dengan file yang diupload", response.Message, StringComparison.Ordinal);
        Assert.Empty(repository.Batches);
        Assert.Equal(0, transaction.CommitCount);
    }

    [Fact]
    public async Task Upload_refuses_when_no_confirmation_accompanies_the_workbook()
    {
        var (service, repository, _) = CreateService(Analysis(1, 0));

        var response = await service.UploadAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            true, "user", null, CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Empty(repository.Batches);
    }

    [Fact]
    public async Task Preflight_reports_the_hash_the_upload_has_to_quote_back()
    {
        var (service, _, _) = CreateService(Analysis(1, 0));

        var response = await service.PreflightAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            CancellationToken.None);

        Assert.Equal(ConfirmedSha(), response.Data!.FileSha256);
    }

    private static StockStagingRowDto AreaRow(int? areaId, StockCondition condition, decimal qtyPcs,
        bool valid = true) => new()
    {
        SourceRowNumber = 3, ProductId = "P", ProductName = "Row", QtyPcs = qtyPcs, IsValid = valid,
        StockAreaId = areaId, Condition = condition
    };

    private static PublishedStockArea Published(int? areaId, string? name, int rows, string by = "siti") => new()
    {
        StockAreaId = areaId, AreaName = name, RowCount = rows, UploadedBy = by,
        UploadedDate = new DateTime(2026, 8, 9), OriginalFileName = "old.xlsx"
    };

    [Fact]
    public async Task Preflight_lists_each_uploaded_area_with_good_and_bad_totals()
    {
        var analysis = Analysis(0, 0);
        analysis.Rows =
        [
            AreaRow(1, StockCondition.Good, 10m), AreaRow(1, StockCondition.Good, 5m),
            AreaRow(1, StockCondition.Bad, 2m), AreaRow(2, StockCondition.Good, 7m),
            AreaRow(2, StockCondition.Bad, 99m, valid: false)
        ];
        var (service, _, _) = CreateService(analysis, areas:
        [
            new StockArea { Id = 1, Name = "Medan" }, new StockArea { Id = 2, Name = "Binjai" }
        ]);

        var response = await service.PreflightAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            CancellationToken.None);

        var areas = response.Data!.Areas;
        Assert.Equal(["Binjai", "Medan"], areas.Select(area => area.AreaName));
        Assert.Equal((1, 7m, 0m), (areas[0].ValidRows, areas[0].QtyPcsGood, areas[0].QtyPcsBad));
        Assert.Equal((3, 15m, 2m), (areas[1].ValidRows, areas[1].QtyPcsGood, areas[1].QtyPcsBad));
    }

    [Fact]
    public async Task Preflight_names_what_each_area_replaces_and_leaves_other_areas_untouched()
    {
        var analysis = Analysis(0, 0);
        analysis.Rows = [AreaRow(1, StockCondition.Good, 10m)];
        var medan = Published(1, "Medan", 40);
        var binjai = Published(2, "Binjai", 25);
        var (service, _, _) = CreateService(analysis, published: [binjai, medan],
            areas: [new StockArea { Id = 1, Name = "Medan" }]);

        var response = await service.PreflightAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            CancellationToken.None);

        Assert.Same(medan, Assert.Single(response.Data!.Areas[0].Replaces));
        Assert.Equal(["Binjai"], response.Data.UntouchedAreaNames);
    }

    [Fact]
    public async Task Preflight_for_a_company_without_areas_shows_the_no_area_snapshot_it_replaces()
    {
        var analysis = Analysis(2, 0);
        var earlier = Published(null, null, 120);
        var (service, _, _) = CreateService(analysis, published: [earlier]);

        var response = await service.PreflightAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            CancellationToken.None);

        var area = Assert.Single(response.Data!.Areas);
        Assert.Equal("(tanpa area)", area.AreaName);
        Assert.Equal(2, area.ValidRows);
        Assert.Same(earlier, Assert.Single(area.Replaces));
        Assert.Empty(response.Data.UntouchedAreaNames);
    }

    [Fact]
    public async Task Preflight_names_active_areas_that_will_still_have_no_stock_for_the_date()
    {
        // Medan is uploaded, Binjai was published earlier; only Tebing still has nothing for the date,
        // and the inactive area is not one anybody is waiting for.
        var analysis = Analysis(0, 0);
        analysis.Rows = [AreaRow(1, StockCondition.Good, 10m)];
        var (service, _, _) = CreateService(analysis, published: [Published(2, "Binjai", 25)], areas:
        [
            new StockArea { Id = 1, Name = "Medan" }, new StockArea { Id = 2, Name = "Binjai" },
            new StockArea { Id = 3, Name = "Tebing" }, new StockArea { Id = 4, Name = "Lama", IsActive = false }
        ]);

        var response = await service.PreflightAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            CancellationToken.None);

        Assert.Equal(["Tebing"], response.Data!.MissingAreaNames);
    }

    [Fact]
    public async Task Preflight_says_when_a_sheet_counts_every_row_as_good_for_want_of_a_condition_column()
    {
        var analysis = MultiSheetAnalysis();
        analysis.Sheets[1].MapsCondition = true;
        var (service, _, _) = CreateService(analysis);

        var response = await service.PreflightAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            CancellationToken.None);

        Assert.Equal(["Stok Gudang"], response.Data!.WorksheetsWithoutCondition);
    }

    [Fact]
    public async Task Preflight_with_workbook_errors_does_not_look_up_published_areas()
    {
        var analysis = Analysis(1, 0);
        analysis.Errors = ["No mapping matched."];
        var (service, repository, _) = CreateService(analysis, published: [Published(1, "Medan", 1)]);

        var response = await service.PreflightAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7,
            CancellationToken.None);

        Assert.Equal(0, repository.PublishedAreaQueries);
        Assert.Empty(response.Data!.Areas);
        Assert.Empty(response.Data.UntouchedAreaNames);
    }

    [Fact]
    public async Task Preflight_still_performs_no_writes()
    {
        var analysis = Analysis(0, 0);
        analysis.Rows = [AreaRow(1, StockCondition.Good, 10m)];
        var (service, repository, transaction) = CreateService(analysis,
            published: [Published(1, "Medan", 4)], areas: [new StockArea { Id = 1, Name = "Medan" }]);

        await service.PreflightAsync(File(), "stock.xlsx", new DateTime(2026, 8, 10), 7, CancellationToken.None);

        Assert.Equal(1, repository.PublishedAreaQueries);
        Assert.Empty(repository.Batches);
        Assert.Empty(repository.StagingRows);
        Assert.Empty(repository.PublishedSourceRows);
        Assert.Equal(0, transaction.CommitCount);
    }

    private static MemoryStream File() => new([1, 2, 3]);

    /// <summary>The SHA-256 preflight reports for the bytes File() supplies, quoted back on upload.</summary>
    private static string ConfirmedSha() =>
        Convert.ToHexString(SHA256.HashData([1, 2, 3])).ToLowerInvariant();

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
        public IReadOnlyList<PublishedStockArea> PublishedAreas { get; set; } = [];
        public int PublishedAreaQueries { get; private set; }
        public Task<IReadOnlyList<PublishedStockArea>> GetPublishedAreasAsync(int companyId, DateTime stockDate,
            CancellationToken cancellationToken)
        { PublishedAreaQueries++; return Task.FromResult(PublishedAreas); }
        public Task<long> InsertBatchAsync(StockUploadBatch batch, CancellationToken cancellationToken)
        {
            batch.Id = 9; Batches.Add(batch); return Task.FromResult(9L);
        }
        public Task InsertStagingAsync(IReadOnlyCollection<StockStaging> rows, CancellationToken cancellationToken)
        { StagingRows.AddRange(rows); return Task.CompletedTask; }
        public string? ArchivedFilePath { get; private set; }

    public Task SetArchivedFilePathAsync(long batchId, string archivedFilePath,
        CancellationToken cancellationToken)
    {
        ArchivedFilePath = archivedFilePath;
        Batches.Single(value => value.Id == batchId).ArchivedFilePath = archivedFilePath;
        return Task.CompletedTask;
    }

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

internal sealed class StubStockAreaRepository(IReadOnlyList<StockArea> areas) : IStockAreaRepository
{
    public Task<IReadOnlyList<StockArea>> ListAsync(int companyId, bool activeOnly,
        CancellationToken cancellationToken) => Task.FromResult(areas);
    public Task<StockArea?> GetAsync(int id, int companyId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    public Task<int> InsertAsync(StockArea area, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    public Task UpdateAsync(StockArea area, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
    public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<bool> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
