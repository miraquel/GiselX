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
    IOptions<SelloutUploadOptions> options) : ISelloutUploadService
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
        var data = ToPreflight(analysis);
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
        int companyId, bool multiWeekConfirmed, string uploadedBy, CancellationToken cancellationToken)
    {
        var contentResponse = await ReadRequestAsync(file, originalFileName, cancellationToken);
        if (contentResponse.Error is not null) return Failure(contentResponse.Error);
        var content = contentResponse.Content!;
        var analysis = await analysisService.AnalyzeAsync(content, companyId, cancellationToken);
        if (analysis.IsMultiWeek && !multiWeekConfirmed) return Failure(MultiWeekConfirmationMessage);

        var invalidRows = analysis.Rows.Count(row => !row.IsValid);
        var group = new SelloutUploadGroup
        {
            CompanyId = companyId,
            DistributorTemplateMappingId = analysis.Mapping?.Id,
            OriginalFileName = Path.GetFileName(originalFileName),
            FileSha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            Status = SelloutUploadGroupStatus.Processing,
            TotalRows = analysis.Rows.Count,
            ValidRows = analysis.Rows.Count - invalidRows,
            InvalidRows = invalidRows,
            IgnoredSummaryRowsJson = JsonSerializer.Serialize(analysis.IgnoredSummaryRowNumbers),
            ValidationErrorsJson = JsonSerializer.Serialize(analysis.RowErrors),
            UploadedBy = uploadedBy,
            UploadedDate = DateTime.UtcNow
        };
        group.Id = await uploadRepository.InsertGroupAsync(group, cancellationToken);

        if (!analysis.IsValid)
        {
            var message = analysis.Errors.Count > 0
                ? string.Join(" ", analysis.Errors)
                : $"{invalidRows} row(s) failed validation.";
            await uploadRepository.MarkGroupFailedAsync(
                group.Id,
                analysis.Rows.Count,
                invalidRows,
                message,
                group.ValidationErrorsJson,
                cancellationToken);
            transaction.Commit();
            return FailedGroup(group.Id, message, analysis.RowErrors, analysis);
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

        foreach (var row in analysis.Rows)
        {
            row.SelloutUploadBatchId = batchIds[SelloutWorkbookParser.GetMonday(row.InvoiceDate!.Value)];
        }
        await uploadRepository.InsertStagingAsync(analysis.Rows.ToList(), cancellationToken);
        await uploadRepository.PromoteGroupAsync(group.Id, cancellationToken);
        transaction.Commit();
        return new ServiceResponse<SelloutUploadResultDto>
        {
            Data = new SelloutUploadResultDto
            {
                GroupId = group.Id,
                Status = SelloutUploadBatchStatus.Completed,
                TotalRows = analysis.Rows.Count,
                ValidRows = analysis.Rows.Count,
                Weeks = analysis.WeekSummaries,
                IgnoredSummaryRowNumbers = analysis.IgnoredSummaryRowNumbers,
                Message = "Sellout data was replaced successfully for every reporting week in the workbook."
            },
            Message = "Sellout data was replaced successfully for every reporting week in the workbook."
        };
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
                Message = batch.ErrorSummary ?? (batch.Status == SelloutUploadBatchStatus.Completed ? "Upload completed." : string.Empty),
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

    private async Task<(byte[]? Content, string? Error)> ReadRequestAsync(
        Stream file,
        string filename,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(Path.GetExtension(filename), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return (null, "Only .xlsx files are supported.");
        try
        {
            var content = await ReadBoundedAsync(file, options.Value.MaxFileSizeBytes, cancellationToken);
            return content.Length == 0 ? (null, "The workbook is empty.") : (content, null);
        }
        catch (InvalidDataException exception)
        {
            return (null, exception.Message);
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

    private static SelloutUploadPreflightDto ToPreflight(SelloutWorkbookAnalysisDto analysis) => new()
    {
        MappingName = analysis.Mapping?.Name,
        TotalRows = analysis.Rows.Count,
        ValidRows = analysis.Rows.Count(row => row.IsValid),
        InvalidRows = analysis.Rows.Count(row => !row.IsValid),
        Weeks = analysis.WeekSummaries,
        IgnoredSummaryRowNumbers = analysis.IgnoredSummaryRowNumbers,
        RowErrors = analysis.RowErrors,
        Errors = analysis.Errors
    };

    private static List<string> CollectErrors(SelloutWorkbookAnalysisDto analysis) =>
        analysis.Errors
            .Concat(analysis.RowErrors.Select(error =>
                $"Row {error.SourceRowNumber}, {error.Field}: {error.Reason}"))
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
        Message = group.ErrorSummary ?? (group.Status == SelloutUploadGroupStatus.Completed
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
        Status = batch.Status == SelloutUploadBatchStatus.Completed
            ? SelloutUploadGroupStatus.Completed
            : batch.Status == SelloutUploadBatchStatus.Failed ? SelloutUploadGroupStatus.Failed : SelloutUploadGroupStatus.Processing,
        OriginalFileName = batch.OriginalFileName,
        FileSha256 = batch.FileSha256,
        TotalRows = batch.TotalRows,
        ValidRows = batch.ValidRows,
        InvalidRows = batch.InvalidRows,
        Message = batch.ErrorSummary ?? (batch.Status == SelloutUploadBatchStatus.Completed ? "Upload completed." : string.Empty),
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

    private static IReadOnlyList<SelloutStagingErrorDto> RowsToErrors(IEnumerable<SelloutStaging> rows) =>
        rows.SelectMany(row => Deserialize<SelloutStagingErrorDto>(row.ValidationErrorsJson)).ToList();

    private static IReadOnlyList<T> Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
