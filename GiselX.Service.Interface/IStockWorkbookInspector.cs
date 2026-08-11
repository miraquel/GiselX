using GiselX.Service.Dto;

namespace GiselX.Service.Interface;

public interface IStockWorkbookInspector
{
    Task<SelloutWorkbookPreviewDto> InspectAsync(byte[] workbook, string? worksheetName,
        int headerRowNumber, int firstDataRowNumber, CancellationToken cancellationToken = default);
    Task<StockMappingDetectionResultDto> DetectAsync(byte[] workbook,
        IReadOnlyCollection<StockTemplateMappingDto> mappings,
        CancellationToken cancellationToken = default);
}
