using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using Riok.Mapperly.Abstractions;

namespace GiselX.Service;

[Mapper]
public partial class MapperlyMapper
{
    // PagedListRequest
    public partial PagedListRequest MapToEntity(PagedListRequestDto pagedListRequestDto);
    public partial PagedListRequestDto MapToDto(PagedListRequest pagedListRequest);
    
    // ListRequest
    public partial ListRequest MapToEntity(ListRequestDto listRequestDto);
    public partial ListRequestDto MapToDto(ListRequest listRequest);
    
    // ServiceLevel
    public partial ServiceLevel MapToEntity(ServiceLevelDto serviceLevelDto);
    public partial IEnumerable<ServiceLevel> MapToEntity(IEnumerable<ServiceLevelDto> serviceLevelDtos);
    public partial PagedList<ServiceLevel> MapToEntity(PagedListDto<ServiceLevelDto> pagedListDto);
    public partial ServiceLevelDto MapToDto(ServiceLevel serviceLevel);
    public partial IEnumerable<ServiceLevelDto> MapToDto(IEnumerable<ServiceLevel> serviceLevels);
    public partial PagedListDto<ServiceLevelDto> MapToDto(PagedList<ServiceLevel> pagedListDto);
    
    // Period
    public partial Period MapToEntity(PeriodDto periodDto);
    public partial IEnumerable<Period> MapToEntity(IEnumerable<PeriodDto> periodDtos);
    public partial PeriodDto MapToDto(Period period);
    public partial IEnumerable<PeriodDto> MapToDto(IEnumerable<Period> periods);
    
    // Company
    public partial Company MapToEntity(CompanyDto companyDto);
    public partial IEnumerable<Company> MapToEntity(IEnumerable<CompanyDto> companyDtos);
    public partial PagedList<Company> MapToEntity(PagedListDto<CompanyDto> pagedListDto);
    public partial CompanyDto MapToDto(Company company);
    public partial IEnumerable<CompanyDto> MapToDto(IEnumerable<Company> companies);
    public partial PagedListDto<CompanyDto> MapToDto(PagedList<Company> pagedListDto);
}