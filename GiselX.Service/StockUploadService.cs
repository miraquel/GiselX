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
    IOptions<StockUploadOptions> options) : IStockUploadService
{
    public async Task<ServiceResponse<StockUploadPreflightDto>> PreflightAsync(Stream file,
        string originalFileName, DateTime stockDate, int companyId, CancellationToken cancellationToken)
    {
        var request = await ReadRequestAsync(file, originalFileName, stockDate, cancellationToken);
        if (request.Error is not null) return PreflightFailure(request.Error);
        var analysis = await analysisService.AnalyzeAsync(request.Content!, companyId, cancellationToken);
        return new ServiceResponse<StockUploadPreflightDto>
        {
            Data = ToPreflight(analysis, stockDate.Date),
            Errors = analysis.Errors.ToList(),
            Message = analysis.Errors.Count == 0 ? "Workbook analysis completed." : "Workbook validation failed.",
            StatusCode = analysis.Errors.Count == 0 ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest
        };
    }

    public async Task<ServiceResponse<StockUploadResultDto>> UploadAsync(Stream file, string originalFileName,
        DateTime stockDate, int companyId, bool confirmed, string uploadedBy, CancellationToken cancellationToken)
    {
        if (!confirmed) return Failure("Review the workbook and confirm the Stock upload before continuing.");
        var request = await ReadRequestAsync(file, originalFileName, stockDate, cancellationToken);
        if (request.Error is not null) return Failure(request.Error);

        var content = request.Content!;
        var analysis = await analysisService.AnalyzeAsync(content, companyId, cancellationToken);
        var batch = new StockUploadBatch
        {
            CompanyId = companyId,
            StockTemplateMappingId = analysis.Mapping?.Id,
            StockDate = stockDate.Date,
            OriginalFileName = Path.GetFileName(originalFileName),
            FileSha256 = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant(),
            TotalRows = analysis.Rows.Count,
            ValidRows = analysis.ValidRows,
            InvalidRows = analysis.InvalidRows,
            IgnoredSummaryRowsJson = JsonSerializer.Serialize(analysis.IgnoredSummaryRowNumbers),
            ValidationErrorsJson = JsonSerializer.Serialize(analysis.RowErrors),
            UploadedBy = uploadedBy,
            UploadedDate = DateTime.UtcNow
        };
        batch.Id = await repository.InsertBatchAsync(batch, cancellationToken);

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
        var successMessage = status == StockUploadBatchStatus.Completed
            ? "Stock snapshot uploaded successfully."
            : $"Stock snapshot uploaded with {analysis.InvalidRows} invalid row(s) excluded from the final Stock table.";
        return new ServiceResponse<StockUploadResultDto>
        {
            Data = Result(batch, status, successMessage, analysis),
            Message = successMessage
        };
    }

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

    private async Task<(byte[]? Content, string? Error)> ReadRequestAsync(Stream file, string filename,
        DateTime stockDate, CancellationToken cancellationToken)
    {
        if (stockDate == default) return (null, "Stock date is required.");
        if (!string.Equals(Path.GetExtension(filename), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return (null, "Only .xlsx files are supported.");
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
            return output.Length == 0 ? (null, "The workbook is empty.") : (output.ToArray(), null);
        }
        catch (InvalidDataException exception) { return (null, exception.Message); }
    }

    private static StockStaging ToStaging(StockStagingRowDto row, long batchId) => new()
    {
        StockUploadBatchId = batchId, SourceRowNumber = row.SourceRowNumber, ProductId = row.ProductId,
        ProductName = row.ProductName, DistributorProductId = row.DistributorProductId,
        DistributorProductName = row.DistributorProductName, ItemMappingId = row.ItemMappingId,
        ProductPackaging = row.ProductPackaging,
        ProductPcsInCtn = row.ProductPcsInCtn, ProductNetto = row.ProductNetto, ProductUnit = row.ProductUnit,
        PriceUnit = row.PriceUnit, SaldoAwal = row.SaldoAwal, SaldoMasukPO = row.SaldoMasukPO,
        SaldoAkhir = row.SaldoAkhir,
        BatchNumber = row.BatchNumber, ExpiredDate = row.ExpiredDate, IsValid = row.IsValid,
        ValidationErrorsJson = row.ValidationErrorsJson
    };

    private static StockUploadPreflightDto ToPreflight(StockWorkbookAnalysisDto analysis, DateTime stockDate) => new()
    {
        MappingName = analysis.Mapping?.Name, StockDate = stockDate, TotalRows = analysis.Rows.Count,
        ValidRows = analysis.ValidRows, InvalidRows = analysis.InvalidRows,
        IgnoredSummaryRowNumbers = analysis.IgnoredSummaryRowNumbers,
        RowErrors = analysis.RowErrors, Errors = analysis.Errors
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
