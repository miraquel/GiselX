using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface IPowerBiReportMenuRepository
{
    Task<IReadOnlyList<PowerBiReport>> ListActiveByCompanyAsync(int companyId, CancellationToken token);
}
