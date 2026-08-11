using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Service;

public sealed class StockWorkbookAnalysisService(
    IStockTemplateMappingRepository mappingRepository,
    IStockWorkbookInspector inspector,
    StockWorkbookParser parser) : IStockWorkbookAnalysisService
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
        return new StockWorkbookAnalysisDto
        {
            Mapping = detection.Mapping,
            Rows = parsed.Rows,
            IgnoredSummaryRowNumbers = parsed.IgnoredSummaryRowNumbers,
            RowErrors = parsed.Errors,
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
