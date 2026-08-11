using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Service;

public sealed class SelloutWorkbookAnalysisService(
    IDistributorTemplateMappingRepository mappingRepository,
    ISelloutWorkbookInspector inspector,
    SelloutWorkbookParser parser) : ISelloutWorkbookAnalysisService
{
    public async Task<SelloutWorkbookAnalysisDto> AnalyzeAsync(
        byte[] content,
        int companyId,
        CancellationToken cancellationToken)
    {
        var mappings = (await mappingRepository.GetActiveByCompanyAsync(companyId, cancellationToken))
            .Select(ToDto)
            .ToList();
        var detection = await inspector.DetectAsync(content, mappings, cancellationToken);
        if (!detection.IsMatch)
        {
            return new SelloutWorkbookAnalysisDto { Errors = detection.Errors };
        }

        var parsed = parser.Parse(content, detection.Mapping!);
        var errors = parsed.BatchError is null ? Array.Empty<string>() : [parsed.BatchError];
        return new SelloutWorkbookAnalysisDto
        {
            Mapping = detection.Mapping,
            Rows = parsed.Rows,
            WeekSummaries = parsed.WeekSummaries,
            IgnoredSummaryRowNumbers = parsed.IgnoredSummaryRowNumbers,
            RowErrors = parsed.Errors,
            Errors = errors
        };
    }

    private static DistributorTemplateMappingDto ToDto(DistributorTemplateMapping mapping) => new()
    {
        Id = mapping.Id,
        CompanyId = mapping.CompanyId,
        Name = mapping.Name,
        WorksheetName = mapping.WorksheetName,
        HeaderRowNumber = mapping.HeaderRowNumber,
        FirstDataRowNumber = mapping.FirstDataRowNumber,
        HeaderSignature = mapping.HeaderSignature,
        IsActive = mapping.IsActive,
        Columns = mapping.Columns.Select(column => new DistributorTemplateColumnDto
        {
            SourceColumnNumber = column.SourceColumnNumber,
            SourceHeader = column.SourceHeader,
            CanonicalField = column.CanonicalField
        }).ToList()
    };
}
