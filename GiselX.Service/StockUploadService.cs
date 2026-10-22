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
    IUploadArchive archive,
    IStockAreaRepository areaRepository) : IStockUploadService
{
    public async Task<ServiceResponse<StockUploadPreflightDto>> PreflightAsync(Stream file,
        string originalFileName, DateTime stockDate, int companyId, CancellationToken cancellationToken)
    {
        var request = await ReadRequestAsync(file, originalFileName, stockDate, cancellationToken);
        if (request.Error is not null) return PreflightFailure(request.Error);
        var analysis = await analysisService.AnalyzeAsync(request.Content!, companyId, cancellationToken);
        var data = ToPreflight(analysis, stockDate.Date, request.SourceSha256!);

        // Read-only: shows what publishing would replace. Skipped when the workbook is refused,
        // because nothing would be published and the lists would only mislead.
        if (analysis.Errors.Count == 0)
        {
            var published = await repository.GetPublishedAreasAsync(companyId, stockDate.Date, cancellationToken);
            var registered = await areaRepository.ListAsync(companyId, false, cancellationToken);
            var names = registered.ToDictionary(area => area.Id, area => area.Name);
            (data.Areas, data.UntouchedAreaNames) = DescribeAreas(analysis, published, names);

            // An area nobody has uploaded for this date yet is the one most likely forgotten: a second
            // location's file that arrives later, or never. Saying so here costs nothing.
            var covered = analysis.Rows.Where(row => row.IsValid).Select(row => row.StockAreaId)
                .Concat(published.Select(entry => entry.StockAreaId)).ToHashSet();
            data.MissingAreaNames = registered.Where(area => area.IsActive && !covered.Contains(area.Id))
                .Select(area => area.Name).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToList();
        }

        return new ServiceResponse<StockUploadPreflightDto>
        {
            Data = data,
            Errors = analysis.Errors.ToList(),
            Message = analysis.Errors.Count == 0 ? "Analisis file selesai." : "File tidak lolos validasi.",
            StatusCode = analysis.Errors.Count == 0 ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest
        };
    }

    public async Task<ServiceResponse<StockUploadResultDto>> UploadAsync(Stream file, string originalFileName,
        DateTime stockDate, int companyId, bool confirmed, string uploadedBy, string? confirmedSha256,
        CancellationToken cancellationToken)
    {
        if (!confirmed) return Failure(UploadConfirmation.NotConfirmed);
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
                : "Tidak ada baris Stock valid yang bisa disimpan.";
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
            ? "Snapshot Stock berhasil diupload."
            : $"Snapshot Stock berhasil diupload; {analysis.InvalidRows:N0} baris tidak valid tidak disimpan.";
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
            return NotFound("Batch upload Stock tidak ditemukan.");

        var batch = found.Value.Batch;
        if (string.IsNullOrWhiteSpace(batch.ArchivedFilePath))
            return NotFound("File asli upload ini tidak diarsipkan.");

        var content = await archive.OpenAsync(batch.ArchivedFilePath, cancellationToken);
        if (content is null)
            return NotFound("File arsip sudah tidak tersedia di server.");

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
                Errors = ["Batch upload Stock tidak ditemukan."], StatusCode = StatusCodes.Status404NotFound
            };
        var (batch, rows) = found.Value;
        return new ServiceResponse<StockUploadBatchDto>
        {
            Data = new StockUploadBatchDto
            {
                BatchId = batch.Id, CompanyId = batch.CompanyId, StockDate = batch.StockDate,
                OriginalFileName = batch.OriginalFileName, FileSha256 = batch.FileSha256, Status = batch.Status,
                TotalRows = batch.TotalRows, ValidRows = batch.ValidRows, InvalidRows = batch.InvalidRows,
                Message = batch.ErrorSummary ?? (batch.InvalidRows > 0
                    ? $"Upload selesai; {batch.InvalidRows:N0} baris tidak valid tidak disimpan."
                    : "Upload selesai."), UploadedBy = batch.UploadedBy,
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
        if (stockDate == default) return (null, null, "Tanggal stok wajib diisi.");
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
                    throw new InvalidDataException(UploadMessages.FileTooLarge(options.Value.MaxFileSizeBytes));
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            }
            if (output.Length == 0) return (null, null, UploadMessages.EmptyFile);

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
        StockAreaId = row.StockAreaId, Condition = row.Condition,
        SourceAreaText = row.SourceAreaText, SourceConditionText = row.SourceConditionText,
        ProductPcsInCtn = row.ProductPcsInCtn, ProductNetto = row.ProductNetto, ProductUnit = row.ProductUnit,
        SmallestUnit = row.SmallestUnit, SmallestUnitQty = row.SmallestUnitQty,
        PriceUnit = row.PriceUnit, SaldoAwal = row.SaldoAwal, SaldoMasukPO = row.SaldoMasukPO,
        SaldoAkhir = row.SaldoAkhir, QtyCarton = row.QtyCarton, QtyPcs = row.QtyPcs,
        BatchNumber = row.BatchNumber, ExpiredDate = row.ExpiredDate, IsValid = row.IsValid,
        ValidationErrorsJson = row.ValidationErrorsJson
    };

    private const string NoAreaName = "(tanpa area)";

    /// <summary>
    /// The scope of a publication is the areas carried by valid rows, so that is what is listed and
    /// what is matched against the published entries; every other published area is untouched.
    /// </summary>
    private static (IReadOnlyList<StockAreaReplacementDto> Areas, IReadOnlyList<string> Untouched) DescribeAreas(
        StockWorkbookAnalysisDto analysis, IReadOnlyList<PublishedStockArea> published,
        IReadOnlyDictionary<int, string> names)
    {
        string NameOf(int? areaId) => areaId is null
            ? NoAreaName
            : published.FirstOrDefault(entry => entry.StockAreaId == areaId)?.AreaName
              ?? (names.TryGetValue(areaId.Value, out var name) ? name : $"Area {areaId}");

        var areas = analysis.Rows.Where(row => row.IsValid).GroupBy(row => row.StockAreaId)
            .Select(group => new StockAreaReplacementDto
            {
                AreaName = NameOf(group.Key),
                ValidRows = group.Count(),
                QtyCartonGood = group.Where(row => row.Condition == StockCondition.Good).Sum(row => row.QtyCarton ?? 0m),
                QtyPcsGood = group.Where(row => row.Condition == StockCondition.Good).Sum(row => row.QtyPcs ?? 0m),
                QtyCartonBad = group.Where(row => row.Condition == StockCondition.Bad).Sum(row => row.QtyCarton ?? 0m),
                QtyPcsBad = group.Where(row => row.Condition == StockCondition.Bad).Sum(row => row.QtyPcs ?? 0m),
                Replaces = published.Where(entry => entry.StockAreaId == group.Key).ToList()
            })
            // "(no area)" sorts first, the rest alphabetically.
            .OrderBy(area => area.AreaName == NoAreaName ? 0 : 1)
            .ThenBy(area => area.AreaName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        var scope = analysis.Rows.Where(row => row.IsValid).Select(row => row.StockAreaId).ToHashSet();
        var untouched = published.Where(entry => !scope.Contains(entry.StockAreaId))
            .Select(entry => entry.AreaName ?? NoAreaName).Distinct().ToList();
        return (areas, untouched);
    }

    private static StockUploadPreflightDto ToPreflight(StockWorkbookAnalysisDto analysis, DateTime stockDate,
        string fileSha256) => new()
    {
        FileSha256 = fileSha256,
        MappingName = DescribeMappings(analysis), StockDate = stockDate, TotalRows = analysis.Rows.Count,
        ValidRows = analysis.ValidRows, InvalidRows = analysis.InvalidRows,
        TotalQtyCarton = analysis.Rows.Where(row => row.IsValid).Sum(row => row.QtyCarton ?? 0m),
        TotalQtyPcs = analysis.Rows.Where(row => row.IsValid).Sum(row => row.QtyPcs ?? 0m),
        Sheets = analysis.Sheets, IgnoredWorksheetNames = analysis.IgnoredWorksheetNames,
        WorksheetsWithoutCondition = analysis.Sheets.Where(sheet => !sheet.MapsCondition)
            .Select(sheet => sheet.WorksheetName).ToList(),
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
