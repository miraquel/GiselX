using GiselX.Domain;
using GiselX.Domain.Common;

namespace GiselX.Repository.Interface;

public interface ICompanyRepository
{
    // CRUD Operations
    Task<Company> CreateCompanyAsync(Company company, CancellationToken cancellationToken);
    Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken);
    Task<PagedList<Company>> GetCompaniesAsync(PagedListRequest pagedListRequest, CancellationToken cancellationToken);
    Task<Company> UpdateCompanyAsync(Company company, CancellationToken cancellationToken);
}