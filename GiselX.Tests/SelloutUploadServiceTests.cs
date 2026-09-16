using System.Security.Cryptography;
using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

public sealed class SelloutUploadServiceTests
{
    [Fact]
    public async Task PreflightAsync_returns_analysis_without_writing()
    {
        var analysis = ValidAnalysis(multiWeek: true);
        var (service, repository, transaction) = CreateService(analysis);

        var response = await service.PreflightAsync(Stream(), "sales.xlsx", 9, CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.True(response.Data!.IsMultiWeek);
        Assert.Equal(2, response.Data.Weeks.Count);
        Assert.Empty(repository.Groups);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task PreflightAsync_summarizes_values_from_valid_rows_only()
    {
        var analysis = ValidAnalysis(multiWeek: false);
        analysis.Rows[0].InvoiceQty = 2m;
        analysis.Rows[0].UnitPrice = 10m;
        analysis.Rows[0].DiscountValue = 1m;
        analysis.Rows[1].InvoiceQty = 3m;
        analysis.Rows[1].GrossValue = 45m;
        analysis.Rows[1].DiscountValue = 5m;
        analysis.Rows[1].NetValue = 38m;
        analysis.Rows = analysis.Rows.Append(new SelloutStaging
        {
            SourceRowNumber = 5, IsValid = false, InvoiceQty = 999m,
            GrossValue = 999m, DiscountValue = 999m, NetValue = 999m
        }).ToList();
        var (service, _, _) = CreateService(analysis);

        var response = await service.PreflightAsync(Stream(), "sales.xlsx", 9, CancellationToken.None);

        Assert.Equal(5m, response.Data!.TotalInvoiceQty);
        Assert.Equal(65m, response.Data.TotalGrossValue);
        Assert.Equal(6m, response.Data.TotalDiscountValue);
        Assert.Equal(57m, response.Data.TotalNetValue);
    }

    [Fact]
    public async Task UploadAsync_requires_explicit_confirmation_for_multiple_weeks()
    {
        var (service, repository, transaction) = CreateService(ValidAnalysis(multiWeek: true));

        var response = await service.UploadAsync(
            Stream(), "sales.xlsx", 9, multiWeekConfirmed: false, "uploader", ConfirmedSha(), CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains("multiple reporting weeks", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(repository.Groups);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task UploadAsync_creates_one_batch_per_week_assigns_rows_and_commits_once()
    {
        var (service, repository, transaction) = CreateService(ValidAnalysis(multiWeek: true));

        var response = await service.UploadAsync(
            Stream(), "sales.xlsx", 9, multiWeekConfirmed: true, "uploader", ConfirmedSha(), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(50, response.Data!.GroupId);
        Assert.Equal([new DateTime(2026, 6, 29), new DateTime(2026, 7, 6)],
            repository.Batches.Select(batch => batch.ReportingWeekStart));
        Assert.Equal([101L, 102L], repository.StagingRows.Select(row => row.SelloutUploadBatchId));
        Assert.Equal(50, repository.PromotedGroupId);
        Assert.True(transaction.CommitCalled);
        Assert.Equal(1, transaction.CommitCount);
    }

    [Fact]
    public async Task UploadAsync_refuses_the_workbook_when_the_invalid_share_reaches_the_limit()
    {
        var analysis = ValidAnalysis(multiWeek: false);
        analysis.RowErrors =
        [
            new SelloutStagingErrorDto
            {
                SourceRowNumber = 4, Field = SelloutCanonicalField.InvoiceDate,
                Reason = "A value is required."
            }
        ];
        analysis.Rows[1].IsValid = false;
        var (service, repository, transaction) = CreateService(analysis);

        var response = await service.UploadAsync(
            Stream(), "sales.xlsx", 9, multiWeekConfirmed: false, "uploader", ConfirmedSha(), CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Empty(repository.Batches);
        Assert.Empty(repository.StagingRows);
        Assert.Null(repository.PromotedGroupId);
        Assert.Equal(1, transaction.CommitCount);
    }

    [Fact]
    public async Task UploadAsync_forwards_both_item_identities_and_mapping_id_to_sales_staging()
    {
        var analysis = ValidAnalysis(multiWeek: false);
        analysis.Rows[0].ProductId = "4100039";
        analysis.Rows[0].ProductName = "GMK Product";
        analysis.Rows[0].DistributorProductId = "P-DIST";
        analysis.Rows[0].DistributorProductName = "Distributor Product";
        analysis.Rows[0].ItemMappingId = 9;
        var (service, repository, _) = CreateService(analysis);

        var response = await service.UploadAsync(
            Stream(), "sales.xlsx", 9, multiWeekConfirmed: false, "uploader", ConfirmedSha(), CancellationToken.None);

        Assert.True(response.IsSuccess);
        var staging = repository.StagingRows.First();
        Assert.Equal("4100039", staging.ProductId);
        Assert.Equal("GMK Product", staging.ProductName);
        Assert.Equal("P-DIST", staging.DistributorProductId);
        Assert.Equal("Distributor Product", staging.DistributorProductName);
        Assert.Equal(9, staging.ItemMappingId);
    }

    [Fact]
    public async Task UploadAsync_does_not_commit_when_atomic_promotion_fails()
    {
        var (service, repository, transaction) = CreateService(ValidAnalysis(multiWeek: true));
        repository.ThrowOnPromote = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(
            Stream(), "sales.xlsx", 9, multiWeekConfirmed: true, "uploader", ConfirmedSha(), CancellationToken.None));

        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task GetUploadResultAsync_returns_all_batches_in_the_group()
    {
        var (service, repository, _) = CreateService(ValidAnalysis(multiWeek: true));
        repository.GroupResult = (
            new SelloutUploadGroup
            {
                Id = 50, CompanyId = 9, Status = SelloutUploadGroupStatus.Completed,
                OriginalFileName = "sales.xlsx", UploadedBy = "uploader",
                UploadedDate = new DateTime(2026, 7, 10), TotalRows = 3, ValidRows = 3,
                IgnoredSummaryRowsJson = "[8]",
                ValidationErrorsJson = "[{\"SourceRowNumber\":9,\"Field\":12,\"Reason\":\"Invalid date.\"}]"
            },
            [
                new SelloutUploadBatch { Id = 101, ReportingWeekStart = new DateTime(2026, 6, 29), TotalRows = 1, ValidRows = 1 },
                new SelloutUploadBatch { Id = 102, ReportingWeekStart = new DateTime(2026, 7, 6), TotalRows = 2, ValidRows = 2 }
            ],
            []);

        var response = await service.GetUploadResultAsync(50, null, 9, CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(50, response.Data!.GroupId);
        Assert.Equal(2, response.Data.Batches.Count);
        Assert.Equal([1, 2], response.Data.Batches.Select(batch => batch.TotalRows));
        Assert.Equal([8], response.Data.IgnoredSummaryRowNumbers);
        Assert.Equal(9, Assert.Single(response.Data.Errors).SourceRowNumber);
    }

    [Fact]
    public async Task UploadAsync_publishes_the_valid_rows_when_only_a_few_are_invalid()
    {
        // One bad cell in twenty-one rows is 4.8%, under the 5% limit: the month still goes through.
        var analysis = AnalysisOf(Rows(valid: 20, invalid: 1, week: new DateTime(2026, 7, 4)));
        var (service, repository, transaction) = CreateService(analysis);

        var response = await service.UploadAsync(
            Stream(), "sales.xlsx", 9, multiWeekConfirmed: false, "uploader", ConfirmedSha(), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(SelloutUploadBatchStatus.CompletedWithWarnings, response.Data!.Status);
        Assert.Equal(1, response.Data.InvalidRows);
        Assert.Equal(50, repository.PromotedGroupId);
        // The invalid row is staged beside the valid ones: it is the record of what was excluded.
        Assert.Equal(21, repository.StagingRows.Count);
        Assert.Single(repository.StagingRows, row => !row.IsValid);
        Assert.Equal(1, transaction.CommitCount);
    }

    [Fact]
    public async Task UploadAsync_names_the_proportion_and_the_limit_when_it_refuses()
    {
        var analysis = AnalysisOf(Rows(valid: 8, invalid: 2, week: new DateTime(2026, 7, 4)));
        var (service, repository, _) = CreateService(analysis);

        var response = await service.UploadAsync(
            Stream(), "sales.xlsx", 9, multiWeekConfirmed: false, "uploader", ConfirmedSha(), CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains("20%", response.Message, StringComparison.Ordinal);
        Assert.Contains("5% limit", response.Message, StringComparison.Ordinal);
        Assert.Empty(repository.StagingRows);
        Assert.Null(repository.PromotedGroupId);
    }

    [Fact]
    public async Task UploadAsync_refuses_a_single_invalid_row_when_the_limit_is_zero()
    {
        // Zero is the escape hatch back to the older all-or-nothing behaviour.
        var analysis = AnalysisOf(Rows(valid: 20, invalid: 1, week: new DateTime(2026, 7, 4)));
        var (service, repository, _) = CreateService(analysis, maxInvalidRowPercentage: 0m);

        var response = await service.UploadAsync(
            Stream(), "sales.xlsx", 9, multiWeekConfirmed: false, "uploader", ConfirmedSha(), CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Empty(repository.StagingRows);
        Assert.Null(repository.PromotedGroupId);
    }

    [Fact]
    public async Task UploadAsync_reports_an_invalid_row_that_has_no_date_without_staging_it()
    {
        // A missing invoice date is frequently the very reason a row is invalid, and it leaves the row
        // with no reporting week to be staged against.
        var rows = Rows(valid: 20, invalid: 0, week: new DateTime(2026, 7, 4))
            .Append((InvoiceDate: (DateTime?)null, IsValid: false)).ToArray();
        var (service, repository, _) = CreateService(AnalysisOf(rows));

        var response = await service.UploadAsync(
            Stream(), "sales.xlsx", 9, multiWeekConfirmed: false, "uploader", ConfirmedSha(), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(20, repository.StagingRows.Count);
        // The group still carries the row in its counts, so nothing is lost from the audit.
        Assert.Equal(21, Assert.Single(repository.Groups).TotalRows);
        Assert.Equal(1, repository.Groups[0].InvalidRows);
    }

    [Fact]
    public async Task UploadAsync_does_not_stage_an_invalid_row_from_a_week_no_valid_row_reached()
    {
        var rows = Rows(valid: 20, invalid: 0, week: new DateTime(2026, 7, 4))
            .Append((InvoiceDate: (DateTime?)new DateTime(2026, 8, 15), IsValid: false)).ToArray();
        var (service, repository, _) = CreateService(AnalysisOf(rows));

        var response = await service.UploadAsync(
            Stream(), "sales.xlsx", 9, multiWeekConfirmed: false, "uploader", ConfirmedSha(), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Single(repository.Batches);
        Assert.Equal(20, repository.StagingRows.Count);
    }

    [Fact]
    public async Task UploadAsync_completes_cleanly_when_every_row_is_valid()
    {
        var analysis = AnalysisOf(Rows(valid: 5, invalid: 0, week: new DateTime(2026, 7, 4)));
        var (service, _, _) = CreateService(analysis);

        var response = await service.UploadAsync(
            Stream(), "sales.xlsx", 9, multiWeekConfirmed: false, "uploader", ConfirmedSha(), CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(SelloutUploadBatchStatus.Completed, response.Data!.Status);
        Assert.Equal(0, response.Data.InvalidRows);
    }

    private static (DateTime? InvoiceDate, bool IsValid)[] Rows(int valid, int invalid, DateTime week) =>
        Enumerable.Repeat(((DateTime?)week, true), valid)
            .Concat(Enumerable.Repeat(((DateTime?)week, false), invalid))
            .ToArray();

    /// <summary>
    /// Builds an analysis the way the real service does, in particular deriving the reporting weeks from
    /// valid rows that carry a date — which is what leaves some invalid rows with no week to belong to.
    /// </summary>

    /// <summary>Mirrors SelloutWorkbookParser.GetMonday, which is internal to the service assembly.</summary>
    private static DateTime MondayOf(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static SelloutWorkbookAnalysisDto AnalysisOf((DateTime? InvoiceDate, bool IsValid)[] rows)
    {
        var staged = rows.Select((row, index) => new SelloutStaging
        {
            SourceRowNumber = index + 3, InvoiceDate = row.InvoiceDate, IsValid = row.IsValid
        }).ToList();
        return new SelloutWorkbookAnalysisDto
        {
            Mapping = new DistributorTemplateMappingDto { Id = 4, Name = "Mapping Sellout" },
            Rows = staged,
            WeekSummaries = staged
                .Where(row => row.IsValid && row.InvoiceDate.HasValue)
                .GroupBy(row => MondayOf(row.InvoiceDate!.Value))
                .OrderBy(group => group.Key)
                .Select(group => new SelloutWeekSummaryDto
                {
                    ReportingWeekStart = group.Key, RowCount = group.Count()
                })
                .ToList()
        };
    }

    [Fact]
    public async Task A_completed_upload_notifies_with_the_group_totals_and_its_weeks()
    {
        var notifications = new RecordingUploadNotificationDispatcher();
        var (service, repository, _) = CreateService(
            ValidAnalysis(multiWeek: false), notifications: notifications);

        var response = await service.UploadAsync(Stream(), "sales.xlsx", 9, multiWeekConfirmed: true,
            "budi", ConfirmedSha(), CancellationToken.None);

        Assert.True(response.IsSuccess);
        var notification = Assert.Single(notifications.Enqueued);
        Assert.Equal(9, notification.CompanyId);
        Assert.Equal(UploadNotificationKind.Sellout, notification.Kind);
        Assert.Equal("budi", notification.UploadedBy);
        Assert.Equal("sales.xlsx", notification.OriginalFileName);
        Assert.Equal(repository.Groups.Single().Id, notification.Reference);
        Assert.StartsWith("Minggu ", notification.PeriodLabel);
    }

    [Fact]
    public async Task A_multi_week_upload_names_the_range_it_replaced()
    {
        var notifications = new RecordingUploadNotificationDispatcher();
        var (service, _, _) = CreateService(ValidAnalysis(multiWeek: true), notifications: notifications);

        await service.UploadAsync(Stream(), "sales.xlsx", 9, multiWeekConfirmed: true,
            "budi", ConfirmedSha(), CancellationToken.None);

        var notification = Assert.Single(notifications.Enqueued);
        Assert.Contains(" minggu, ", notification.PeriodLabel);
    }

    [Fact]
    public async Task A_workbook_refused_for_too_many_invalid_rows_notifies_nobody()
    {
        // One row of two is invalid, which is above a 0% tolerance, so nothing is published.
        var notifications = new RecordingUploadNotificationDispatcher();
        var analysis = AnalysisOf([
            (new DateTime(2026, 7, 4), true),
            (new DateTime(2026, 7, 5), false)
        ]);
        var (service, _, _) = CreateService(
            analysis, maxInvalidRowPercentage: 0m, notifications: notifications);

        await service.UploadAsync(Stream(), "sales.xlsx", 9, multiWeekConfirmed: true,
            "budi", ConfirmedSha(), CancellationToken.None);

        Assert.Empty(notifications.Enqueued);
    }

    private static (SelloutUploadService Service, FakeUploadRepository Repository, FakeTransaction Transaction)
        CreateService(SelloutWorkbookAnalysisDto analysis, decimal maxInvalidRowPercentage = 5m,
            IUploadNotificationDispatcher? notifications = null)
    {
        var repository = new FakeUploadRepository();
        var transaction = new FakeTransaction();
        var service = new SelloutUploadService(
            repository,
            new FakeAnalysisService(analysis),
            transaction,
            Options.Create(new SelloutUploadOptions
            {
                MaxFileSizeBytes = 1024, MaxInvalidRowPercentage = maxInvalidRowPercentage
            }),
            notifications ?? new RecordingUploadNotificationDispatcher());
        return (service, repository, transaction);
    }

    [Fact]
    public async Task UploadAsync_refuses_a_workbook_that_is_not_the_one_that_was_confirmed()
    {
        var (service, repository, transaction) = CreateService(ValidAnalysis(multiWeek: false));
        var otherWorkbook = Convert.ToHexString(SHA256.HashData([9, 9, 9])).ToLowerInvariant();

        var response = await service.UploadAsync(Stream(), "sales.xlsx", 9, multiWeekConfirmed: false,
            "uploader", otherWorkbook, CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains("not the workbook that was uploaded", response.Message, StringComparison.Ordinal);
        Assert.Empty(repository.Groups);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task UploadAsync_refuses_when_no_confirmation_accompanies_the_workbook()
    {
        var (service, repository, _) = CreateService(ValidAnalysis(multiWeek: false));

        var response = await service.UploadAsync(Stream(), "sales.xlsx", 9, multiWeekConfirmed: false,
            "uploader", null, CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Empty(repository.Groups);
    }

    [Fact]
    public async Task PreflightAsync_reports_the_hash_the_upload_has_to_quote_back()
    {
        var (service, _, _) = CreateService(ValidAnalysis(multiWeek: false));

        var response = await service.PreflightAsync(Stream(), "sales.xlsx", 9, CancellationToken.None);

        Assert.Equal(ConfirmedSha(), response.Data!.FileSha256);
    }

    private static MemoryStream Stream() => new([1, 2, 3, 4]);

    /// <summary>The SHA-256 preflight reports for the bytes Stream() supplies, quoted back on upload.</summary>
    private static string ConfirmedSha() =>
        Convert.ToHexString(SHA256.HashData([1, 2, 3, 4])).ToLowerInvariant();

    private static SelloutWorkbookAnalysisDto ValidAnalysis(bool multiWeek)
    {
        var firstDate = new DateTime(2026, 7, 4);
        var secondDate = multiWeek ? new DateTime(2026, 7, 6) : new DateTime(2026, 7, 5);
        return new SelloutWorkbookAnalysisDto
        {
            Mapping = new DistributorTemplateMappingDto { Id = 4, Name = "Mapping Sellout" },
            Rows =
            [
                new SelloutStaging { SourceRowNumber = 3, InvoiceDate = firstDate, IsValid = true },
                new SelloutStaging { SourceRowNumber = 4, InvoiceDate = secondDate, IsValid = true }
            ],
            WeekSummaries = multiWeek
                ?
                [
                    new SelloutWeekSummaryDto { ReportingWeekStart = new DateTime(2026, 6, 29), RowCount = 1 },
                    new SelloutWeekSummaryDto { ReportingWeekStart = new DateTime(2026, 7, 6), RowCount = 1 }
                ]
                : [new SelloutWeekSummaryDto { ReportingWeekStart = new DateTime(2026, 6, 29), RowCount = 2 }]
        };
    }

    private sealed class FakeAnalysisService(SelloutWorkbookAnalysisDto result) : ISelloutWorkbookAnalysisService
    {
        public int CallCount { get; private set; }

        public Task<SelloutWorkbookAnalysisDto> AnalyzeAsync(byte[] content, int companyId, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeUploadRepository : ISelloutUploadRepository
    {
        public List<SelloutUploadGroup> Groups { get; } = [];
        public List<SelloutUploadBatch> Batches { get; } = [];
        public List<SelloutStaging> StagingRows { get; } = [];
        public long? PromotedGroupId { get; private set; }
        public bool ThrowOnPromote { get; set; }
        public (SelloutUploadGroup Group, IReadOnlyList<SelloutUploadBatch> Batches, IReadOnlyList<SelloutStaging> Rows)? GroupResult { get; set; }

        public Task<long> InsertGroupAsync(SelloutUploadGroup group, CancellationToken cancellationToken)
        {
            group.Id = 50;
            Groups.Add(group);
            return Task.FromResult(group.Id);
        }

        public Task<long> InsertBatchAsync(SelloutUploadBatch batch, CancellationToken cancellationToken)
        {
            batch.Id = 101 + Batches.Count;
            Batches.Add(batch);
            return Task.FromResult(batch.Id);
        }

        public Task InsertStagingAsync(IReadOnlyCollection<SelloutStaging> rows, CancellationToken cancellationToken)
        {
            StagingRows.AddRange(rows);
            return Task.CompletedTask;
        }

        public Task PromoteGroupAsync(long groupId, CancellationToken cancellationToken)
        {
            if (ThrowOnPromote) throw new InvalidOperationException("Promotion failed.");
            PromotedGroupId = groupId;
            return Task.CompletedTask;
        }

        public Task MarkGroupFailedAsync(long groupId, int totalRows, int invalidRows, string errorSummary,
            string validationErrorsJson, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<(SelloutUploadGroup Group, IReadOnlyList<SelloutUploadBatch> Batches, IReadOnlyList<SelloutStaging> Rows)?> GetGroupAsync(
            long groupId, int companyId, CancellationToken cancellationToken) => Task.FromResult(GroupResult);

        public Task SetBatchMappingAsync(long batchId, int mappingId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task MarkFailedAsync(long batchId, int totalRows, int invalidRows, string errorSummary, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task PromoteAsync(long batchId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<(SelloutUploadBatch Batch, IReadOnlyList<SelloutStaging> Rows)?> GetBatchAsync(long batchId, int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeTransaction : IDbTransaction
    {
        public int CommitCount { get; private set; }
        public bool CommitCalled => CommitCount > 0;
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() => CommitCount++;
        public void Dispose() { }
        public void Rollback() { }
    }
}
