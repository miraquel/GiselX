using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace GiselX.Service;

public sealed class SelloutUploadService(
    ISelloutUploadRepository uploadRepository,
    ISelloutWorkbookAnalysisService analysisService,
    IDbTransaction transaction,
    IOptions<SelloutUploadOptions> options,
    IUploadNotificationDispatcher notifications) : ISelloutUploadService
{
    private const string MultiWeekConfirmationMessage =
        "This workbook contains multiple reporting weeks. Review the affected weeks and confirm the upload before continuing.";

    public async Task<ServiceResponse<SelloutUploadPreflightDto>> PreflightAsync(
        Stream file,
        string originalFileName,
        int companyId,
        CancellationToken cancellationToken)
    {
        var contentResponse = await ReadRequestAsync(file, originalFileName, cancellationToken);
        if (contentResponse.Error is not null)
        {
            return new ServiceResponse<SelloutUploadPreflightDto>
            {
                Errors = [contentResponse.Error],
                Message = contentResponse.Error,
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var analysis = await analysisService.AnalyzeAsync(contentResponse.Content!, companyId, cancellationToken);
        var data = ToPreflight(analysis, contentResponse.SourceSha256!);
        var errors = CollectErrors(analysis);
        return new ServiceResponse<SelloutUploadPreflightDto>
        {
            Data = data,
            Errors = errors,
            Message = errors.Count == 0 ? "Workbook analysis completed." : "Workbook validation failed.",
            StatusCode = errors.Count == 0 ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest
        };
    }

    public async Task<ServiceResponse<SelloutUploadResultDto>> UploadAsync(Stream file, string originalFileName,
        int companyId, bool multiWeekConfirmed, string uploadedBy, string? confirmedSha256,
        CancellationToken cancellationToken)
    {
        var contentResponse = await ReadRequestAsync(file, originalFileName, cancellationToken);
        if (contentResponse.Error is not null) return Failure(contentResponse.Error);

        var mismatch = UploadConfirmation.DescribeMismatch(confirmedSha256, contentResponse.SourceSha256!);
        if (mismatch is not null) return Failure(mismatch);
        var content = contentResponse.Content!;
        var analysis = await analysisService.AnalyzeAsync(content, companyId, cancellationToken);
        if (analysis.IsMultiWeek && !multiWeekConfirmed) return Failure(MultiWeekConfirmationMessage);

        var totalRows = analysis.Rows.Count;
        var invalidRows = analysis.Rows.Count(row => !row.IsValid);
        var validRows = totalRows - invalidRows;
        var group = new SelloutUploadGroup
        {
            CompanyId = companyId,
            DistributorTemplateMappingId = analysis.Mapping?.Id,
            OriginalFileName = Path.GetFileName(originalFileName),
            FileSha256 = contentResponse.SourceSha256!,
            Status = SelloutUploadGroupStatus.Processing,
            TotalRows = totalRows,
            ValidRows = validRows,
            InvalidRows = invalidRows,
            IgnoredSummaryRowsJson = JsonSerializer.Serialize(analysis.IgnoredSummaryRowNumbers),
            ValidationErrorsJson = JsonSerializer.Serialize(analysis.RowErrors),
            UploadedBy = uploadedBy,
            UploadedDate = DateTime.UtcNow
        };
        group.Id = await uploadRepository.InsertGroupAsync(group, cancellationToken);

        var refusal = DescribeRefusal(analysis, totalRows, validRows, invalidRows);
        if (refusal is not null)
        {
            await uploadRepository.MarkGroupFailedAsync(
                group.Id, totalRows, invalidRows, refusal, group.ValidationErrorsJson, cancellationToken);
            transaction.Commit();
            return FailedGroup(group.Id, refusal, analysis.RowErrors, analysis);
        }

        var batchIds = new Dictionary<DateTime, long>();
        foreach (var week in analysis.WeekSummaries.OrderBy(week => week.ReportingWeekStart))
        {
            var batch = new SelloutUploadBatch
            {
                CompanyId = companyId,
                DistributorTemplateMappingId = analysis.Mapping!.Id,
                SelloutUploadGroupId = group.Id,
                ReportingWeekStart = week.ReportingWeekStart,
                OriginalFileName = group.OriginalFileName,
                FileSha256 = group.FileSha256,
                Status = SelloutUploadBatchStatus.Processing,
                TotalRows = week.RowCount,
                ValidRows = week.RowCount,
                UploadedBy = uploadedBy,
                UploadedDate = group.UploadedDate
            };
            batch.Id = await uploadRepository.InsertBatchAsync(batch, cancellationToken);
            batchIds.Add(week.ReportingWeekStart, batch.Id);
        }

        // Week batches are derived from rows that carry a usable invoice date, so an invalid row can only
        // be staged when its own date lands in a week some valid row also reached. A row with no date -
        // frequently the very reason it is invalid - has no week to belong to, and neither has a row whose
        // week contributed nothing valid. Those are reported through the group's own error list, which
        // already carries every row error the analysis produced, rather than being forced onto a batch
        // they did not come from.
        var staging = new List<SelloutStaging>(totalRows);
        foreach (var row in analysis.Rows)
        {
            if (!row.InvoiceDate.HasValue) continue;
            if (!batchIds.TryGetValue(SelloutWorkbookParser.GetMonday(row.InvoiceDate.Value), out var batchId)) continue;

            row.SelloutUploadBatchId = batchId;
            staging.Add(row);
        }

        await uploadRepository.InsertStagingAsync(staging, cancellationToken);
        await uploadRepository.PromoteGroupAsync(group.Id, cancellationToken);
        transaction.Commit();

        // Queued only after the commit: a notification for rows that rolled back would be a lie.
        notifications.Enqueue(new UploadNotificationDto
        {
            CompanyId = companyId,
            Kind = UploadNotificationKind.Sellout,
            Reference = group.Id,
            TotalRows = totalRows,
            ValidRows = validRows,
            InvalidRows = invalidRows,
            UploadedBy = uploadedBy,
            CompletedAtUtc = DateTime.UtcNow,
            OriginalFileName = group.OriginalFileName,
            PeriodLabel = DescribeWeeks(analysis)
        });

        var status = invalidRows > 0
            ? SelloutUploadBatchStatus.CompletedWithWarnings
            : SelloutUploadBatchStatus.Completed;
        var message = invalidRows > 0
            ? $"Sellout data was replaced for every reporting week in the workbook, with {invalidRows:N0} invalid row(s) excluded."
            : "Sellout data was replaced successfully for every reporting week in the workbook.";
        return new ServiceResponse<SelloutUploadResultDto>
        {
            Data = new SelloutUploadResultDto
            {
                GroupId = group.Id,
                Status = status,
                TotalRows = totalRows,
                ValidRows = validRows,
                InvalidRows = invalidRows,
                Weeks = analysis.WeekSummaries,
                IgnoredSummaryRowNumbers = analysis.IgnoredSummaryRowNumbers,
                Errors = analysis.RowErrors,
                Message = message
            },
            Message = message
        };
    }

    /// <summary>
    /// Names the reporting weeks the upload replaced, because "Sellout upload received" without the
    /// period does not tell a reader whether the week they care about was the one that moved. A
    /// multi-week workbook is described by its range rather than listing every Monday.
    /// </summary>
    private static string? DescribeWeeks(SelloutWorkbookAnalysisDto analysis)
    {
        var weeks = analysis.WeekSummaries
            .Select(week => week.ReportingWeekStart)
            .OrderBy(start => start)
            .ToList();

        return weeks.Count switch
        {
            0 => null,
            1 => $"Week of {weeks[0]:dd MMMM yyyy}",
            _ => $"{weeks.Count} weeks, {weeks[0]:dd MMMM yyyy} to {weeks[^1]:dd MMMM yyyy}"
        };
    }

    /// <summary>
    /// Why this workbook cannot be published at all, or null when its valid rows should go through. A few
    /// unreadable cells no longer reject a month of sellout, but a workbook whose invalid share reaches
    /// the configured limit is a mapping or export problem rather than a few bad cells, and publishing
    /// part of it would quietly replace whole reporting weeks with less than they should hold.
    /// </summary>
    private string? DescribeRefusal(SelloutWorkbookAnalysisDto analysis, int totalRows, int validRows,
        int invalidRows)
    {
        if (analysis.Errors.Count > 0) return string.Join(" ", analysis.Errors);
        if (analysis.Mapping is null) return "No active distributor mapping matches this workbook.";
        if (totalRows == 0) return "The workbook contains no data rows.";
        if (validRows == 0) return $"All {totalRows:N0} row(s) failed validation. No data was published.";
        if (invalidRows == 0) return null;

        var limit = options.Value.MaxInvalidRowPercentage;
        var share = invalidRows * 100m / totalRows;
        return share < limit
            ? null
            : $"{invalidRows:N0} of {totalRows:N0} row(s) ({share:0.#}%) failed validation, which is at or "
              + $"above the {limit:0.#}% limit. No data was published.";
    }

    public async Task<ServiceResponse<SelloutUploadBatchDto>> GetBatchAsync(long batchId, int companyId, CancellationToken cancellationToken)
    {
        var found = await uploadRepository.GetBatchAsync(batchId, companyId, cancellationToken);
        if (found is null)
            return new ServiceResponse<SelloutUploadBatchDto> { Errors = ["Upload batch was not found."], StatusCode = StatusCodes.Status404NotFound };

        var (batch, rows) = found.Value;
        var errors = rows.SelectMany(row => JsonSerializer.Deserialize<List<SelloutStagingErrorDto>>(row.ValidationErrorsJson) ?? []).ToList();
        return new ServiceResponse<SelloutUploadBatchDto>
        {
            Data = new SelloutUploadBatchDto
            {
                BatchId = batch.Id, CompanyId = batch.CompanyId, ReportingWeekStart = batch.ReportingWeekStart,
                OriginalFileName = batch.OriginalFileName, FileSha256 = batch.FileSha256, Status = batch.Status,
                TotalRows = batch.TotalRows, ValidRows = batch.ValidRows, InvalidRows = batch.InvalidRows,
                Message = batch.ErrorSummary ?? DescribeBatchCompletion(batch),
                Errors = errors, UploadedBy = batch.UploadedBy, UploadedDate = batch.UploadedDate, CompletedDate = batch.CompletedDate
            }
        };
    }

    public async Task<ServiceResponse<SelloutUploadGroupDto>> GetUploadResultAsync(
        long? groupId, long? batchId, int companyId, CancellationToken cancellationToken)
    {
        if (groupId is > 0)
        {
            var found = await uploadRepository.GetGroupAsync(groupId.Value, companyId, cancellationToken);
            if (found is null) return GroupNotFound();
            var (group, batches, rows) = found.Value;
            return new ServiceResponse<SelloutUploadGroupDto> { Data = MapGroup(group, batches, rows) };
        }

        if (batchId is > 0)
        {
            var legacy = await uploadRepository.GetBatchAsync(batchId.Value, companyId, cancellationToken);
            if (legacy is null) return GroupNotFound();
            var (batch, rows) = legacy.Value;
            return new ServiceResponse<SelloutUploadGroupDto>
            {
                Data = MapLegacyBatch(batch, rows)
            };
        }

        return GroupNotFound();
    }

    private async Task<(byte[]? Content, string? SourceSha256, string? Error)> ReadRequestAsync(
        Stream file,
        string filename,
        CancellationToken cancellationToken)
    {
        if (!LegacyExcelWorkbookConverter.IsSupportedExtension(filename))
            return (null, null, LegacyExcelWorkbookConverter.UnsupportedFileMessage);
        try
        {
            var source = await ReadBoundedAsync(file, options.Value.MaxFileSizeBytes, cancellationToken);
            if (source.Length == 0) return (null, null, "The workbook is empty.");

            // Hash what the distributor actually sent; a converted .xls is not the audited artifact.
            var sha256 = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
            return (LegacyExcelWorkbookConverter.EnsureOpenXml(source), sha256, null);
        }
        catch (InvalidDataException exception)
        {
            return (null, null, exception.Message);
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(Stream source, long maximum, CancellationToken cancellationToken)
    {
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (output.Length + read > maximum) throw new InvalidDataException($"The workbook exceeds the {maximum} byte upload limit.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return output.ToArray();
    }

    private static ServiceResponse<SelloutUploadResultDto> Failure(string error) => new()
    {
        Errors = [error], Message = error, StatusCode = StatusCodes.Status400BadRequest
    };

    private static ServiceResponse<SelloutUploadResultDto> FailedGroup(
        long id,
        string message,
        IReadOnlyList<SelloutStagingErrorDto> errors,
        SelloutWorkbookAnalysisDto analysis) => new()
    {
        Data = new SelloutUploadResultDto
        {
            GroupId = id,
            Status = SelloutUploadBatchStatus.Failed,
            TotalRows = analysis.Rows.Count,
            ValidRows = analysis.Rows.Count(row => row.IsValid),
            InvalidRows = analysis.Rows.Count(row => !row.IsValid),
            Message = message,
            Errors = errors,
            Weeks = analysis.WeekSummaries,
            IgnoredSummaryRowNumbers = analysis.IgnoredSummaryRowNumbers
        },
        Errors = [message], Message = message, StatusCode = StatusCodes.Status400BadRequest
    };

    private static SelloutUploadPreflightDto ToPreflight(SelloutWorkbookAnalysisDto analysis, string fileSha256) => new()
    {
        FileSha256 = fileSha256,
        MappingName = analysis.Mapping?.Name,
        TotalRows = analysis.Rows.Count,
        ValidRows = analysis.Rows.Count(row => row.IsValid),
        InvalidRows = analysis.Rows.Count(row => !row.IsValid),
        TotalInvoiceQty = analysis.Rows.Where(row => row.IsValid).Sum(row => row.InvoiceQty ?? 0m),
        TotalGrossValue = analysis.Rows.Where(row => row.IsValid).Sum(row =>
            row.GrossValue ?? (row.InvoiceQty ?? 0m) * (row.UnitPrice ?? 0m)),
        TotalDiscountValue = analysis.Rows.Where(row => row.IsValid).Sum(row => row.DiscountValue ?? 0m),
        TotalNetValue = analysis.Rows.Where(row => row.IsValid).Sum(row =>
            row.NetValue ??
            (row.GrossValue ?? (row.InvoiceQty ?? 0m) * (row.UnitPrice ?? 0m)) - (row.DiscountValue ?? 0m)),
        Weeks = analysis.WeekSummaries,
        IgnoredSummaryRowNumbers = analysis.IgnoredSummaryRowNumbers,
        RowErrors = analysis.RowErrors,
        Errors = analysis.Errors,
        Warnings = PartialWeekWarnings(analysis.WeekSummaries)
    };

    /// <summary>
    /// Publishing replaces whole Monday-Sunday weeks. Inside the range a workbook covers, that is correct -
    /// a day with no rows genuinely had no sales. The danger is only at the two ends, where a week reaches
    /// past the file's first or last invoice date into a neighbouring period the file says nothing about,
    /// and replacing the whole week deletes rows a previous upload published there.
    /// </summary>
    private static List<string> PartialWeekWarnings(IReadOnlyList<SelloutWeekSummaryDto> weeks)
    {
        if (weeks.Count == 0) return [];

        var firstCoveredDay = weeks.Min(week => week.CoveredFrom);
        var lastCoveredDay = weeks.Max(week => week.CoveredTo);
        var warnings = new List<string>();
        foreach (var week in weeks)
        {
            if (week.ReportingWeekStart < firstCoveredDay)
            {
                warnings.Add(Describe(week, week.ReportingWeekStart, firstCoveredDay.AddDays(-1)));
            }

            if (week.ReportingWeekEnd > lastCoveredDay)
            {
                warnings.Add(Describe(week, lastCoveredDay.AddDays(1), week.ReportingWeekEnd));
            }
        }

        return warnings;
    }

    private static string Describe(SelloutWeekSummaryDto week, DateTime uncoveredFrom, DateTime uncoveredTo) =>
        $"This workbook carries no rows for {uncoveredFrom:yyyy-MM-dd} to {uncoveredTo:yyyy-MM-dd}, but they fall in "
        + $"the reporting week {week.ReportingWeekStart:yyyy-MM-dd} to {week.ReportingWeekEnd:yyyy-MM-dd} that "
        + "confirming replaces in full. Any rows a previous upload published on those days will be deleted.";

    private static List<string> CollectErrors(SelloutWorkbookAnalysisDto analysis) =>
        analysis.Errors
            .Concat(analysis.RowErrors.Select(error =>
                // The rejected text is the whole diagnosis for a bad cell - without it "not a valid
                // DateTime" leaves the user opening the workbook to find out what the cell actually held.
                string.IsNullOrWhiteSpace(error.RejectedValue)
                    ? $"Row {error.SourceRowNumber}, {error.Field}: {error.Reason}"
                    : $"Row {error.SourceRowNumber}, {error.Field}: {error.Reason} (cell value: \"{error.RejectedValue}\")"))
            .ToList();

    private static ServiceResponse<SelloutUploadGroupDto> GroupNotFound() => new()
    {
        Errors = ["Upload result was not found."], StatusCode = StatusCodes.Status404NotFound
    };

    private static SelloutUploadGroupDto MapGroup(SelloutUploadGroup group,
        IReadOnlyList<SelloutUploadBatch> batches, IReadOnlyList<SelloutStaging> rows) => new()
    {
        GroupId = group.Id,
        CompanyId = group.CompanyId,
        Status = group.Status,
        OriginalFileName = group.OriginalFileName,
        FileSha256 = group.FileSha256,
        TotalRows = group.TotalRows,
        ValidRows = group.ValidRows,
        InvalidRows = group.InvalidRows,
        Message = group.ErrorSummary ?? (group.Status is SelloutUploadGroupStatus.Completed or SelloutUploadGroupStatus.CompletedWithWarnings
            ? "Upload completed for every reporting week." : string.Empty),
        UploadedBy = group.UploadedBy,
        UploadedDate = group.UploadedDate,
        CompletedDate = group.CompletedDate,
        IgnoredSummaryRowNumbers = Deserialize<int>(group.IgnoredSummaryRowsJson),
        Errors = Deserialize<SelloutStagingErrorDto>(group.ValidationErrorsJson)
            .Concat(RowsToErrors(rows)).ToList(),
        Batches = batches.Select(batch => MapBatch(batch, rows.Where(row => row.SelloutUploadBatchId == batch.Id))).ToList()
    };

    private static SelloutUploadGroupDto MapLegacyBatch(SelloutUploadBatch batch, IReadOnlyList<SelloutStaging> rows) => new()
    {
        GroupId = batch.SelloutUploadGroupId ?? 0,
        CompanyId = batch.CompanyId,
        Status = batch.Status switch
        {
            SelloutUploadBatchStatus.Completed => SelloutUploadGroupStatus.Completed,
            SelloutUploadBatchStatus.CompletedWithWarnings => SelloutUploadGroupStatus.CompletedWithWarnings,
            SelloutUploadBatchStatus.Failed => SelloutUploadGroupStatus.Failed,
            _ => SelloutUploadGroupStatus.Processing
        },
        OriginalFileName = batch.OriginalFileName,
        FileSha256 = batch.FileSha256,
        TotalRows = batch.TotalRows,
        ValidRows = batch.ValidRows,
        InvalidRows = batch.InvalidRows,
        Message = batch.ErrorSummary ?? DescribeBatchCompletion(batch),
        UploadedBy = batch.UploadedBy,
        UploadedDate = batch.UploadedDate,
        CompletedDate = batch.CompletedDate,
        Errors = RowsToErrors(rows),
        Batches = [MapBatch(batch, rows)]
    };

    private static SelloutUploadBatchDto MapBatch(SelloutUploadBatch batch, IEnumerable<SelloutStaging> rows) => new()
    {
        BatchId = batch.Id,
        GroupId = batch.SelloutUploadGroupId ?? 0,
        CompanyId = batch.CompanyId,
        ReportingWeekStart = batch.ReportingWeekStart,
        OriginalFileName = batch.OriginalFileName,
        FileSha256 = batch.FileSha256,
        Status = batch.Status,
        TotalRows = batch.TotalRows,
        ValidRows = batch.ValidRows,
        InvalidRows = batch.InvalidRows,
        Message = batch.ErrorSummary ?? string.Empty,
        Errors = RowsToErrors(rows),
        UploadedBy = batch.UploadedBy,
        UploadedDate = batch.UploadedDate,
        CompletedDate = batch.CompletedDate
    };

    private static string DescribeBatchCompletion(SelloutUploadBatch batch) => batch.Status switch
    {
        SelloutUploadBatchStatus.Completed => "Upload completed.",
        SelloutUploadBatchStatus.CompletedWithWarnings =>
            $"Upload completed with {batch.InvalidRows:N0} invalid row(s) excluded.",
        _ => string.Empty
    };

    private static IReadOnlyList<SelloutStagingErrorDto> RowsToErrors(IEnumerable<SelloutStaging> rows) =>
        rows.SelectMany(row => Deserialize<SelloutStagingErrorDto>(row.ValidationErrorsJson)).ToList();

    private static IReadOnlyList<T> Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
