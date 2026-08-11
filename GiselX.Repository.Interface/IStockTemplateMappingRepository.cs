using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface IStockTemplateMappingRepository
{
    Task<IReadOnlyList<StockTemplateMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<StockTemplateMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken);
    Task<StockTemplateMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken);
    Task<int> InsertAsync(StockTemplateMapping mapping, CancellationToken cancellationToken);
    Task UpdateAsync(StockTemplateMapping mapping, CancellationToken cancellationToken);
    Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken);
    Task<StockTemplateMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken);
}
