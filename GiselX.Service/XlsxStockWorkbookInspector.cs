using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Service;

public sealed class XlsxStockWorkbookInspector : IStockWorkbookInspector
{
    private readonly XlsxSelloutWorkbookInspector _inner = new();

    public Task<SelloutWorkbookPreviewDto> InspectAsync(byte[] workbook, string? worksheetName,
        int headerRowNumber, int firstDataRowNumber, CancellationToken cancellationToken = default) =>
        _inner.InspectAsync(workbook, worksheetName, headerRowNumber, firstDataRowNumber, cancellationToken);

    public async Task<StockMappingDetectionResultDto> DetectAsync(byte[] workbook,
        IReadOnlyCollection<StockTemplateMappingDto> mappings,
        CancellationToken cancellationToken = default)
    {
        var matches = new List<StockTemplateMappingDto>();
        foreach (var mapping in mappings.Where(mapping => mapping.IsActive))
        {
            var preview = await InspectAsync(workbook, mapping.WorksheetName, mapping.HeaderRowNumber,
                mapping.FirstDataRowNumber, cancellationToken);
            if (preview.Errors.Count == 0 && string.Equals(preview.HeaderSignature, mapping.HeaderSignature,
                    StringComparison.Ordinal))
                matches.Add(mapping);
        }

        if (matches.Count == 1) return new StockMappingDetectionResultDto { Mapping = matches[0] };
        var result = new StockMappingDetectionResultDto
        {
            CandidateMappingNames = matches.Select(mapping => mapping.Name)
                .OrderBy(name => name, StringComparer.Ordinal).ToList()
        };
        result.Errors.Add(matches.Count == 0
            ? "No active Stock mapping exactly matches this workbook."
            : "Multiple active Stock mappings exactly match this workbook.");
        return result;
    }
}
