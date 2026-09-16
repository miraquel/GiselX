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

public sealed class StockUploadService(
    IStockUploadRepository repository,
    IStockWorkbookAnalysisService analysisService,
    IDbTransaction transaction,
    IOptions<StockUploadOptions> options,
    IUploadNotificationDispatcher notifications,
    IUploadArchive archive) : IStockUploadService
{
    public async Task<ServiceResponse<StockUploadPreflightDto>> PreflightAsync(Stream file,
        string originalFileName, DateTime stockDate, int companyId, CancellationToken cancellationToken)
    {
        var request = await ReadRequestAsync(file, originalFileName, stockDate, cancellationToken);
        if (request.Error is not null) return PreflightFailure(request.Error);
        var analysis = await analysisService.AnalyzeAsync(request.Content!, companyId, cancellationToken);
        return new ServiceResponse<StockUploadPreflightDto>
        {
            Data = ToPreflight(analysis, stockDate.Date, request.SourceSha256!),
            Errors = analysis.Errors.ToList(),
            Message = analysis.Errors.Count == 0 ? "Workbook analysis completed." : "Workbook validation failed.",
            StatusCode = analysis.Errors.Count == 0 ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest
        };
    }

    public async Task<ServiceResponse<StockUploadResultDto>> UploadAsync(Stream file, string originalFileName,
        DateTime stockDate, int companyId, bool confirmed, string uploadedBy, string? confirmedSha256,
        CancellationToken cancellationToken)
    {
        if (!confirmed) return Failure("Review the workbook and confirm the Stock upload before continuing.");
        var request = await ReadRequestAsync(file, originalFileName, stockDate, cancellationToken);
        if (request.Error is not null) return Failure(request.Error);

        var mismatch = UploadConfirmation.DescribeMismatch(confirmedSha256, request.SourceSha256!);
        if (mismatch is not null) return Failure(mismatch);

        var content = request.Content!;
        var analysis = await analysisService.AnalyzeAsync(content, companyId, cancellationToken);
        var batch = new StockUploadBatch
        {
            CompanyId = companyId,
            StockTemplateMappingId = analysis.Mapping?.Id,
            StockDate = stockDate.Date,
            OriginalFileName = Path.GetFileName(originalFileName),
            FileSha256 = request.SourceSha256!,
            TotalRows = analysis.Rows.Count,
            ValidRows = analysis.ValidRows,
            InvalidRows = analysis.InvalidRows,
            IgnoredSummaryRowsJson = JsonSerializer.Serialize(analysis.IgnoredSummaryRowNumbers),
            ValidationErrorsJson = JsonSerializer.Serialize(analysis.RowErrors),
            UploadedBy = uploadedBy,
            UploadedDate = DateTime.UtcNow
        };
        batch.Id = await repository.InsertBatchAsync(batch, cancellationToken);

        // Archived once the id exists, and before the commit, so a stored path never outlives a
        // rolled-back batch. A failed write returns null and the upload carries on regardless.
        var archivedPath = await archive.StoreAsync(UploadNotificationKind.Stock, batch.Id,
            batch.OriginalFileName, content, cancellationToken);
        if (archivedPath is not null)
        {
            batch.ArchivedFilePath = archivedPath;
            await repository.SetArchivedFilePathAsync(batch.Id, archivedPath, cancellationToken);
        }

        var staging = analysis.Rows.Select(row => ToStaging(row, batch.Id)).ToList();
        await repository.InsertStagingAsync(staging, cancellationToken);
        if (analysis.ValidRows == 0 || analysis.Errors.Count > 0)
        {
            var message = analysis.Errors.Count > 0
                ? string.Join(" ", analysis.Errors)
                : "No valid Stock rows are available to publish.";
            await repository.MarkFailedAsync(batch.Id, message, cancellationToken);
            transaction.Commit();
            return new ServiceResponse<StockUploadResultDto>
            {
                Data = Result(batch, StockUploadBatchStatus.Failed, message, analysis),
                Errors = [message], Message = message, StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var status = analysis.InvalidRows > 0
            ? StockUploadBatchStatus.CompletedWithWarnings
            : StockUploadBatchStatus.Completed;
        await repository.PublishValidRowsAsync(batch.Id, companyId, status, cancellationToken);
        transaction.Commit();

        // Queued only after the commit: a notification for rows that rolled back would be a lie.
        notifications.Enqueue(new UploadNotificationDto
        {
            CompanyId = companyId,
            Kind = UploadNotificationKind.Stock,
            Reference = batch.Id,
            TotalRows = batch.TotalRows,
            ValidRows = batch.ValidRows,
            InvalidRows = batch.InvalidRows,
            UploadedBy = uploadedBy,
            CompletedAtUtc = DateTime.UtcNow,
            OriginalFileName = batch.OriginalFileName,
            PeriodLabel = $"Snapshot {stockDate.ToString("dd MMMM yyyy", UploadNotificationMessage.Culture)}",
            // No OutletCount: a Stock workbook is the distributor's own warehouse position and
            // carries no customer, so the mail omits that line rather than reporting zero outlets.
            SourceFileArchived = batch.ArchivedFilePath is not null,
            ProductCount = CountDistinctProducts(analysis)
        });

        var successMessage = status == StockUploadBatchStatus.Completed
            ? "Stock snapshot uploaded successfully."
            : $"Stock snapshot uploaded with {analysis.InvalidRows} invalid row(s) excluded from the final Stock table.";
        return new ServiceResponse<StockUploadResultDto>
        {
            Data = Result(batch, status, successMessage, analysis),
            Message = successMessage
        };
    }

    public async Task<ServiceResponse<ArchivedUploadFileDto>> DownloadSourceAsync(long batchId, int companyId,
        CancellationToken cancellationToken)
    {
        // The company id comes from the signed-in user, never from the request, and the lookup is
        // scoped by it — a distributor guessing another batch id gets "not found", not a file.
        var found = await repository.GetBatchAsync(batchId, companyId, cancellationToken);
        if (found is null)
            return NotFound("Stock upload batch was not found.");

        var batch = found.Value.Batch;
        if (string.IsNullOrWhiteSpace(batch.ArchivedFilePath))
            return NotFound("The uploaded file for this batch was not archived.");

        var content = await archive.OpenAsync(batch.ArchivedFilePath, cancellationToken);
        if (content is null)
            return NotFound("The archived file is no longer available on the server.");

        return new ServiceResponse<ArchivedUploadFileDto>
        {
            Data = new ArchivedUploadFileDto { Content = content, FileName = batch.OriginalFileName }
        };
    }

    private static ServiceResponse<ArchivedUploadFileDto> NotFound(string error) => new()
    {
        Errors = [error], Message = error, StatusCode = StatusCodes.Status404NotFound
    };

    public async Task<ServiceResponse<StockUploadBatchDto>> GetResultAsync(long batchId, int companyId,
        CancellationToken cancellationToken)
    {
        var found = await repository.GetBatchAsync(batchId, companyId, cancellationToken);
        if (found is null)
            return new ServiceResponse<StockUploadBatchDto>
            {
                Errors = ["Stock upload batch was not found."], StatusCode = StatusCodes.Status404NotFound
            };
        var (batch, rows) = found.Value;
        return new ServiceResponse<StockUploadBatchDto>
        {
            Data = new StockUploadBatchDto
            {
                BatchId = batch.Id, CompanyId = batch.CompanyId, StockDate = batch.StockDate,
                OriginalFileName = batch.OriginalFileName, FileSha256 = batch.FileSha256, Status = batch.Status,
                TotalRows = batch.TotalRows, ValidRows = batch.ValidRows, InvalidRows = batch.InvalidRows,
                Message = batch.ErrorSummary ?? "Upload completed.", UploadedBy = batch.UploadedBy,
                UploadedDate = batch.UploadedDate, CompletedDate = batch.CompletedDate,
                IgnoredSummaryRowNumbers = Deserialize<int>(batch.IgnoredSummaryRowsJson),
                Errors = rows.SelectMany(row => Deserialize<StockStagingErrorDto>(row.ValidationErrorsJson)).ToList()
            }
        };
    }

    /// <summary>
    /// Distinct products among the rows that were published, or null when none carry an identifier —
    /// the mail then omits the line rather than reporting a misleading zero.
    /// </summary>
    private static int? CountDistinctProducts(StockWorkbookAnalysisDto analysis)
    {
        var count = analysis.Rows
            .Where(row => row.IsValid)
            .Select(row => row.ProductId ?? row.DistributorProductId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();

        return count == 0 ? null : count;
    }

    private async Task<(byte[]? Content, string? SourceSha256, string? Error)> ReadRequestAsync(Stream file,
        string filename, DateTime stockDate, CancellationToken cancellationToken)
    {
        if (stockDate == default) return (null, null, "Stock date is required.");
        if (!LegacyExcelWorkbookConverter.IsSupportedExtension(filename))
            return (null, null, LegacyExcelWorkbookConverter.UnsupportedFileMessage);
        try
        {
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await file.ReadAsync(buffer, cancellationToken)) > 0)
            {
                if (output.Length + read > options.Value.MaxFileSizeBytes)
                    throw new InvalidDataException($"The workbook exceeds the {options.Value.MaxFileSizeBytes} byte upload limit.");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            if (output.Length == 0) return (null, null, "The workbook is empty.");

            // Hash what the distributor actually sent; a converted .xls is not the audited artifact.
            var source = output.ToArray();
            var sha256 = Convert.ToHexString(SHA256.HashData(source)).ToLowerInvariant();
            return (LegacyExcelWorkbookConverter.EnsureOpenXml(source), sha256, null);
        }
        catch (InvalidDataException exception) { return (null, null, exception.Message); }
    }

    private static StockStaging ToStaging(StockStagingRowDto row, long batchId) => new()
    {
        StockUploadBatchId = batchId, StockTemplateMappingId = row.StockTemplateMappingId,
        SourceWorksheetName = row.SourceWorksheetName,
        SourceRowNumber = row.SourceRowNumber, ProductId = row.ProductId,
        ProductName = row.ProductName, DistributorProductId = row.DistributorProductId,
        DistributorProductName = row.DistributorProductName, ItemMappingId = row.ItemMappingId,
        ProductPackaging = row.ProductPackaging,
        ProductPcsInCtn = row.ProductPcsInCtn, ProductNetto = row.ProductNetto, ProductUnit = row.ProductUnit,
        SmallestUnit = row.SmallestUnit, SmallestUnitQty = row.SmallestUnitQty,
        PriceUnit = row.PriceUnit, SaldoAwal = row.SaldoAwal, SaldoMasukPO = row.SaldoMasukPO,
        SaldoAkhir = row.SaldoAkhir,
        BatchNumber = row.BatchNumber, ExpiredDate = row.ExpiredDate, IsValid = row.IsValid,
        ValidationErrorsJson = row.ValidationErrorsJson
    };

    private static StockUploadPreflightDto ToPreflight(StockWorkbookAnalysisDto analysis, DateTime stockDate,
        string fileSha256) => new()
    {
        FileSha256 = fileSha256,
        MappingName = DescribeMappings(analysis), StockDate = stockDate, TotalRows = analysis.Rows.Count,
        ValidRows = analysis.ValidRows, InvalidRows = analysis.InvalidRows,
        TotalSaldoAwal = analysis.Rows.Where(row => row.IsValid).Sum(row => row.SaldoAwal ?? 0m),
        TotalSaldoMasukPo = analysis.Rows.Where(row => row.IsValid).Sum(row => row.SaldoMasukPO ?? 0m),
        TotalSaldoAkhir = analysis.Rows.Where(row => row.IsValid).Sum(row => row.SaldoAkhir ?? 0m),
        Sheets = analysis.Sheets, IgnoredWorksheetNames = analysis.IgnoredWorksheetNames,
        IgnoredSummaryRowNumbers = analysis.IgnoredSummaryRowNumbers,
        RowErrors = analysis.RowErrors, Errors = analysis.Errors
    };

    /// <summary>
    /// Names every mapping the workbook matched, because a single upload can draw on several mapped
    /// worksheets and the review must show all of them, not just the first.
    /// </summary>
    private static string? DescribeMappings(StockWorkbookAnalysisDto analysis) => analysis.Sheets.Count switch
    {
        0 => analysis.Mapping?.Name,
        1 => analysis.Sheets[0].MappingName,
        _ => string.Join(", ", analysis.Sheets.Select(sheet => $"{sheet.MappingName} ({sheet.WorksheetName})"))
    };

    private static StockUploadResultDto Result(StockUploadBatch batch, StockUploadBatchStatus status,
        string message, StockWorkbookAnalysisDto analysis) => new()
    {
        BatchId = batch.Id, Status = status, StockDate = batch.StockDate, TotalRows = analysis.Rows.Count,
        ValidRows = analysis.ValidRows, InvalidRows = analysis.InvalidRows, Message = message,
        IgnoredSummaryRowNumbers = analysis.IgnoredSummaryRowNumbers, Errors = analysis.RowErrors
    };

    private static ServiceResponse<StockUploadPreflightDto> PreflightFailure(string error) => new()
    { Errors = [error], Message = error, StatusCode = StatusCodes.Status400BadRequest };
    private static ServiceResponse<StockUploadResultDto> Failure(string error) => new()
    { Errors = [error], Message = error, StatusCode = StatusCodes.Status400BadRequest };
    private static IReadOnlyList<T> Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json) ?? []; }
        catch (JsonException) { return []; }
    }
}
