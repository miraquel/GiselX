using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface IPowerBiReportRepository
{
    Task<IReadOnlyList<PowerBiReport>> ListAdminAsync(int? companyId, CancellationToken token);
    Task<IReadOnlyList<PowerBiReport>> ListActiveByCompanyAsync(int companyId, CancellationToken token);
    Task<PowerBiReport?> GetAdminByIdAsync(int id, CancellationToken token);
    Task<PowerBiReport?> GetActiveByIdAndCompanyAsync(int id, int companyId, CancellationToken token);
    Task<bool> NameExistsAsync(int companyId, string name, int? excludeId, CancellationToken token);
    Task<int> GetNextDisplayOrderAsync(int companyId, CancellationToken token);
    Task<int> InsertAsync(PowerBiReport report, CancellationToken token);
    Task<bool> UpdateAsync(PowerBiReport report, CancellationToken token);
    Task<bool> SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate, CancellationToken token);
    Task<bool> DeleteAsync(int id, CancellationToken token);
}
