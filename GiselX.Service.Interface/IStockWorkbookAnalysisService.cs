using GiselX.Service.Dto;

namespace GiselX.Service.Interface;

public interface IStockWorkbookAnalysisService
{
    Task<StockWorkbookAnalysisDto> AnalyzeAsync(byte[] content, int companyId,
        CancellationToken cancellationToken);
}
