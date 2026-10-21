using GiselX.Service.Dto;

namespace GiselX.Service.Interface;

public interface ISelloutWorkbookInspector
{
    Task<WorkbookPreviewDto> InspectAsync(
        byte[] workbook,
        string? worksheetName,
        int headerRowNumber,
        int firstDataRowNumber,
        CancellationToken cancellationToken = default);

    Task<SelloutMappingDetectionResultDto> DetectAsync(
        byte[] workbook,
        IReadOnlyCollection<DistributorTemplateMappingDto> mappings,
        CancellationToken cancellationToken = default);
}
