using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface ICompanyService
{
    // CRUD Operations
    Task<ServiceResponse<CompanyDto>> CreateCompanyAsync(CompanyDto companyDto, CancellationToken cancellationToken);
    Task<ServiceResponse<CompanyDto?>> GetCompanyByIdAsync(int id, CancellationToken cancellationToken);
    Task<ServiceResponse<PagedListDto<CompanyDto>>> GetCompaniesAsync(PagedListRequestDto pagedListRequest, CancellationToken cancellationToken);
    Task<ServiceResponse<CompanyDto>> UpdateCompanyAsync(CompanyDto companyDto, CancellationToken cancellationToken);
}