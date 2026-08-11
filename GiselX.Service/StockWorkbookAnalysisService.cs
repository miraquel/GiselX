using System.Text.Json;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Service;

public sealed class StockWorkbookAnalysisService(
    IStockTemplateMappingRepository mappingRepository,
    IStockWorkbookInspector inspector,
    StockWorkbookParser parser,
    IItemMappingResolver itemMappingResolver) : IStockWorkbookAnalysisService
{
    public async Task<StockWorkbookAnalysisDto> AnalyzeAsync(byte[] content, int companyId,
        CancellationToken cancellationToken)
    {
        var mappings = (await mappingRepository.GetActiveByCompanyAsync(companyId, cancellationToken))
            .Select(ToDto).ToList();
        var detection = await inspector.DetectAsync(content, mappings, cancellationToken);
        if (detection.Mapping is null)
            return new StockWorkbookAnalysisDto { Errors = detection.Errors };

        var parsed = parser.Parse(content, detection.Mapping);
        var itemMappingContext = await itemMappingResolver.LoadAsync(companyId, cancellationToken);
        var errors = parsed.Errors.ToList();
        foreach (var row in parsed.Rows)
        {
            var resolution = itemMappingResolver.Resolve(itemMappingContext, row.ProductId, row.ProductName);
            row.DistributorProductId = resolution.DistributorItemId;
            row.DistributorProductName = resolution.DistributorItemName;
            row.ProductId = resolution.GmkItemId;
            row.ProductName = resolution.GmkItemName;
            row.ItemMappingId = resolution.ItemMappingId;
            if (resolution.ErrorReason is null) continue;

            var error = new StockStagingErrorDto
            {
                SourceRowNumber = row.SourceRowNumber,
                Field = StockCanonicalField.ProductId,
                RejectedValue = resolution.DistributorItemId,
                Reason = resolution.ErrorReason
            };
            var rowErrors = JsonSerializer.Deserialize<List<StockStagingErrorDto>>(row.ValidationErrorsJson) ?? [];
            rowErrors.Add(error);
            row.ValidationErrorsJson = JsonSerializer.Serialize(rowErrors);
            row.IsValid = false;
            errors.Add(error);
        }
        return new StockWorkbookAnalysisDto
        {
            Mapping = detection.Mapping,
            Rows = parsed.Rows,
            IgnoredSummaryRowNumbers = parsed.IgnoredSummaryRowNumbers,
            RowErrors = errors,
            Errors = parsed.BatchError is null ? [] : [parsed.BatchError]
        };
    }

    private static StockTemplateMappingDto ToDto(StockTemplateMapping mapping) => new()
    {
        Id = mapping.Id, CompanyId = mapping.CompanyId, Name = mapping.Name,
        WorksheetName = mapping.WorksheetName, HeaderRowNumber = mapping.HeaderRowNumber,
        FirstDataRowNumber = mapping.FirstDataRowNumber, HeaderSignature = mapping.HeaderSignature,
        IsActive = mapping.IsActive,
        Columns = mapping.Columns.Select(column => new StockTemplateColumnDto
        {
            SourceColumnNumber = column.SourceColumnNumber,
            SourceHeader = column.SourceHeader,
            CanonicalField = column.CanonicalField
        }).ToList()
    };
}
