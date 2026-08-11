using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface IDistributorTemplateMappingRepository
{
    Task<IReadOnlyList<DistributorTemplateMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DistributorTemplateMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken);
    Task<DistributorTemplateMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken);
    Task<int> InsertAsync(DistributorTemplateMapping mapping, CancellationToken cancellationToken);
    Task UpdateAsync(DistributorTemplateMapping mapping, CancellationToken cancellationToken);
    Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken);
    Task<DistributorTemplateMappingDeleteResult> DeleteAsync(int id, int companyId,
        CancellationToken cancellationToken);
}
