using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

/// <summary>
/// Publishing replaces whole Monday-Sunday weeks. Inside the range a workbook covers that is correct, so
/// a distributor who never sells on Sundays must not be warned six times a month. Only the two ends,
/// where a week reaches into a period the file says nothing about, risk deleting a previous upload.
/// </summary>
public sealed class SelloutPartialWeekWarningTests
{
    [Fact]
    public async Task A_monthly_workbook_warns_only_about_the_days_outside_its_own_range()
    {
        // The real August export: no Sunday sales in any week, and both ends spill into another month.
        var warnings = await WarnAsync(
            Week("2026-07-27", "2026-08-01", "2026-08-01"),
            Week("2026-08-03", "2026-08-03", "2026-08-08"),
            Week("2026-08-10", "2026-08-10", "2026-08-15"),
            Week("2026-08-17", "2026-08-18", "2026-08-22"),
            Week("2026-08-24", "2026-08-24", "2026-08-29"),
            Week("2026-08-31", "2026-08-31", "2026-08-31"));

        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, warning => warning.Contains("2026-07-27 to 2026-07-31", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Contains("2026-09-01 to 2026-09-06", StringComparison.Ordinal));
    }

    [Fact]
    public async Task An_uncovered_day_inside_the_range_is_not_a_warning()
    {
        // Sunday 09 Aug carries no sales, but the file covers that week, so replacing it is correct.
        var warnings = await WarnAsync(
            Week("2026-08-03", "2026-08-03", "2026-08-08"),
            Week("2026-08-10", "2026-08-10", "2026-08-16"));

        Assert.Empty(warnings);
    }

    [Fact]
    public async Task A_workbook_filling_whole_weeks_warns_about_nothing()
    {
        var warnings = await WarnAsync(
            Week("2026-08-03", "2026-08-03", "2026-08-09"),
            Week("2026-08-10", "2026-08-10", "2026-08-16"));

        Assert.Empty(warnings);
    }

    [Fact]
    public async Task A_single_day_workbook_warns_about_the_rest_of_its_week()
    {
        var warnings = await WarnAsync(Week("2026-08-03", "2026-08-05", "2026-08-05"));

        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, warning => warning.Contains("2026-08-03 to 2026-08-04", StringComparison.Ordinal));
        Assert.Contains(warnings, warning => warning.Contains("2026-08-06 to 2026-08-09", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_workbook_with_no_dated_rows_warns_about_nothing()
    {
        Assert.Empty(await WarnAsync());
    }

    [Fact]
    public async Task A_warning_never_blocks_the_upload()
    {
        var response = await PreflightAsync(Week("2026-08-03", "2026-08-05", "2026-08-05"));

        Assert.True(response.IsSuccess);
        Assert.Empty(response.Errors);
        Assert.NotEmpty(response.Data!.Warnings);
    }

    private static async Task<IReadOnlyList<string>> WarnAsync(params SelloutWeekSummaryDto[] weeks) =>
        (await PreflightAsync(weeks)).Data!.Warnings;

    private static Task<ServiceResponse<SelloutUploadPreflightDto>> PreflightAsync(
        params SelloutWeekSummaryDto[] weeks)
    {
        var analysis = new SelloutWorkbookAnalysisDto
        {
            Mapping = new DistributorTemplateMappingDto { Id = 1, Name = "mapping" },
            Rows = [new SelloutStaging { SourceRowNumber = 2, InvoiceDate = new DateTime(2026, 8, 5), IsValid = true }],
            WeekSummaries = weeks
        };
        var service = new SelloutUploadService(
            new StubRepository(),
            new StubAnalysisService(analysis),
            new StubTransaction(),
            Options.Create(new SelloutUploadOptions { MaxFileSizeBytes = 1024 }));
        return service.PreflightAsync(new MemoryStream([1, 2, 3, 4]), "book.xlsx", 1, CancellationToken.None);
    }

    private static SelloutWeekSummaryDto Week(string weekStart, string coveredFrom, string coveredTo) => new()
    {
        ReportingWeekStart = DateTime.Parse(weekStart),
        CoveredFrom = DateTime.Parse(coveredFrom),
        CoveredTo = DateTime.Parse(coveredTo),
        RowCount = 1
    };

    private sealed class StubAnalysisService(SelloutWorkbookAnalysisDto result) : ISelloutWorkbookAnalysisService
    {
        public Task<SelloutWorkbookAnalysisDto> AnalyzeAsync(byte[] content, int companyId,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class StubTransaction : IDbTransaction
    {
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() { }
        public void Dispose() { }
        public void Rollback() { }
    }

    private sealed class StubRepository : ISelloutUploadRepository
    {
        public Task<long> InsertGroupAsync(SelloutUploadGroup group, CancellationToken cancellationToken) =>
            Task.FromResult(1L);
        public Task<long> InsertBatchAsync(SelloutUploadBatch batch, CancellationToken cancellationToken) =>
            Task.FromResult(1L);
        public Task InsertStagingAsync(IReadOnlyCollection<SelloutStaging> rows, CancellationToken cancellationToken) =>
            Task.CompletedTask;
        public Task PromoteGroupAsync(long groupId, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task MarkGroupFailedAsync(long groupId, int totalRows, int invalidRows, string message,
            string validationErrorsJson, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<(SelloutUploadBatch Batch, IReadOnlyList<SelloutStaging> Rows)?> GetBatchAsync(long batchId,
            int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<(SelloutUploadBatch, IReadOnlyList<SelloutStaging>)?>(null);
        public Task<(SelloutUploadGroup Group, IReadOnlyList<SelloutUploadBatch> Batches, IReadOnlyList<SelloutStaging> Rows)?> GetGroupAsync(
            long groupId, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<(SelloutUploadGroup, IReadOnlyList<SelloutUploadBatch>, IReadOnlyList<SelloutStaging>)?>(null);
    }
}
