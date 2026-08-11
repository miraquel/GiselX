using System.Data;
using GiselX.Mapper;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;

namespace GiselX.Service;

public class CompanyService : ICompanyService
{
    private readonly ICompanyRepository _companyRepository;
    private readonly IItemMappingRepository _itemMappingRepository;
    private readonly IDbTransaction _dbTransaction;
    private readonly UserClaimDto _userClaimDto;
    private readonly MapperlyMapper _mapper = new();

    public CompanyService(ICompanyRepository companyRepository, IItemMappingRepository itemMappingRepository,
        IDbTransaction dbTransaction, UserClaimDto userClaimDto)
    {
        _companyRepository = companyRepository;
        _itemMappingRepository = itemMappingRepository;
        _dbTransaction = dbTransaction;
        _userClaimDto = userClaimDto;
    }

    public async Task<ServiceResponse<CompanyDto>> CreateCompanyAsync(CompanyDto companyDto, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.CreateCompanyAsync(_mapper.MapToEntity(companyDto), cancellationToken);
        
        _dbTransaction.Commit();
        
        return new ServiceResponse<CompanyDto>
        {
            Data = _mapper.MapToDto(company),
            Message = "Company created successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    public async Task<ServiceResponse<CompanyDto?>> GetCompanyByIdAsync(int id, CancellationToken cancellationToken)
    {
        var company = await _companyRepository.GetCompanyByIdAsync(id, cancellationToken);
        
        return new ServiceResponse<CompanyDto?>
        {
            Data = company == null ? null : _mapper.MapToDto(company),
            Message = "Company retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    public async Task<ServiceResponse<PagedListDto<CompanyDto>>> GetCompaniesAsync(PagedListRequestDto pagedListRequest, CancellationToken cancellationToken)
    {
        var companies = await _companyRepository.GetCompaniesAsync(_mapper.MapToEntity(pagedListRequest), cancellationToken);
        
        return new ServiceResponse<PagedListDto<CompanyDto>>
        {
            Data = _mapper.MapToDto(companies),
            Message = "Companies retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    public async Task<ServiceResponse<CompanyDto>> UpdateCompanyAsync(CompanyDto companyDto, CancellationToken cancellationToken)
    {
        var existing = await _companyRepository.GetCompanyByIdAsync(companyDto.Id, cancellationToken);
        if (existing is null)
        {
            return new ServiceResponse<CompanyDto>
            {
                Errors = ["The company was not found."],
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        if (!existing.ItemMappingEnabled && companyDto.ItemMappingEnabled &&
            !(await _itemMappingRepository.GetActiveByCompanyAsync(companyDto.Id, cancellationToken)).Any())
        {
            return new ServiceResponse<CompanyDto>
            {
                Errors = ["At least one active item mapping is required before item mapping can be enabled."],
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var companyToUpdate = _mapper.MapToEntity(companyDto);
        companyToUpdate.ItemMappingEnabled = existing.ItemMappingEnabled || companyToUpdate.ItemMappingEnabled;
        var company = await _companyRepository.UpdateCompanyAsync(companyToUpdate, cancellationToken);
        
        _dbTransaction.Commit();
        
        return new ServiceResponse<CompanyDto>
        {
            Data = _mapper.MapToDto(company),
            Message = "Company updated successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }
}
