using GiselX.Service.Dto;

namespace GiselX.Service.Interface;

public interface ISelloutWorkbookAnalysisService
{
    Task<SelloutWorkbookAnalysisDto> AnalyzeAsync(
        byte[] content,
        int companyId,
        CancellationToken cancellationToken);
}
