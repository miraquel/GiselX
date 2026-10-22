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
    private readonly IBusinessUnitRepository? _businessUnitRepository;
    private readonly MapperlyMapper _mapper = new();

    public CompanyService(ICompanyRepository companyRepository, IItemMappingRepository itemMappingRepository,
        IDbTransaction dbTransaction, UserClaimDto userClaimDto,
        IBusinessUnitRepository? businessUnitRepository = null)
    {
        _companyRepository = companyRepository;
        _itemMappingRepository = itemMappingRepository;
        _dbTransaction = dbTransaction;
        _userClaimDto = userClaimDto;
        _businessUnitRepository = businessUnitRepository;
    }

    public async Task<ServiceResponse<CompanyDto>> CreateCompanyAsync(CompanyDto companyDto, CancellationToken cancellationToken)
    {
        if (ValidateContactEmail(companyDto) is { } contactEmailError) return contactEmailError;
        if (NormalizeLocation(companyDto) is { } locationError) return locationError;
        if (ValidateReminderLeadDays(companyDto) is { } leadDaysError) return leadDaysError;

        if (!await IsSelectableBusinessUnitAsync(companyDto.BusinessUnitId, cancellationToken))
            return InvalidBusinessUnitResponse();

        if (companyDto.ItemMappingEnabled is true)
        {
            return new ServiceResponse<CompanyDto>
            {
                Errors = ["At least one active item mapping is required before item mapping can be enabled."],
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var companyToCreate = _mapper.MapToEntity(companyDto);
        companyToCreate.ItemMappingEnabled = companyDto.ItemMappingEnabled ?? false;
        var company = await _companyRepository.CreateCompanyAsync(companyToCreate, cancellationToken);
        
        _dbTransaction.Commit();
        
        return new ServiceResponse<CompanyDto>
        {
            Data = _mapper.MapToDto(company),
            Message = "Company created successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    /// <summary>
    /// Checks the contact list and rewrites it into its canonical form on the DTO, or returns the
    /// failure response. ContactEmail may hold several addresses delimited by ';' — the reminder job
    /// and the upload notification both send to all of them — so a single bad entry has to be named
    /// rather than reported as "invalid email", and the list must not be stored as typed.
    /// </summary>
    private static ServiceResponse<CompanyDto>? ValidateContactEmail(CompanyDto companyDto)
    {
        var invalid = NotificationRecipients.FindInvalid(companyDto.ContactEmail);
        if (invalid.Count > 0)
        {
            return new ServiceResponse<CompanyDto>
            {
                Errors =
                [
                    $"These are not valid email addresses: {string.Join(", ", invalid)}. " +
                    "Separate multiple addresses with ';'."
                ],
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var normalized = NotificationRecipients.Normalize(companyDto.ContactEmail);
        if (normalized is { Length: > NotificationRecipients.MaxLength })
        {
            return new ServiceResponse<CompanyDto>
            {
                Errors =
                [
                    $"The contact email list must not exceed {NotificationRecipients.MaxLength} characters."
                ],
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        companyDto.ContactEmail = normalized;
        return null;
    }

    /// <summary>
    /// The lead reminder goes out ReminderLeadDays before the deadline. Up to 28 days suits a monthly
    /// deadline; a weekly one allows at most 6, or the lead reminder would arrive before the previous
    /// deadline had passed.
    /// </summary>
    private static ServiceResponse<CompanyDto>? ValidateReminderLeadDays(CompanyDto companyDto)
    {
        var weekly = companyDto.DeadlineDaysOfWeek is > 0 && companyDto.DeadlineDayOfMonth is null;
        var maximum = weekly ? 6 : 28;
        if (companyDto.ReminderLeadDays >= 0 && companyDto.ReminderLeadDays <= maximum) return null;

        return new ServiceResponse<CompanyDto>
        {
            Errors = [$"Reminder lead days must be between 0 and {maximum} for a {(weekly ? "weekly" : "monthly")} deadline."],
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    /// <summary>
    /// Location is free text shown on the Companies screens and in the Power BI views, so it is stored
    /// trimmed, with a blank entry stored as no location rather than as spaces.
    /// </summary>
    private static ServiceResponse<CompanyDto>? NormalizeLocation(CompanyDto companyDto)
    {
        var location = string.IsNullOrWhiteSpace(companyDto.Location) ? null : companyDto.Location.Trim();
        if (location is { Length: > 100 })
        {
            return new ServiceResponse<CompanyDto>
            {
                Errors = ["Location must not exceed 100 characters."],
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        companyDto.Location = location;
        return null;
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
        if (ValidateContactEmail(companyDto) is { } contactEmailError) return contactEmailError;
        if (NormalizeLocation(companyDto) is { } locationError) return locationError;
        if (ValidateReminderLeadDays(companyDto) is { } leadDaysError) return leadDaysError;

        var existing = await _companyRepository.GetCompanyByIdAsync(companyDto.Id, cancellationToken);
        if (existing is null)
        {
            return new ServiceResponse<CompanyDto>
            {
                Errors = ["The company was not found."],
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        if (companyDto.BusinessUnitId != existing.BusinessUnitId &&
            !await IsSelectableBusinessUnitAsync(companyDto.BusinessUnitId, cancellationToken))
            return InvalidBusinessUnitResponse();

        if (!existing.ItemMappingEnabled && companyDto.ItemMappingEnabled is true &&
            !(await _itemMappingRepository.GetActiveByCompanyAsync(companyDto.Id, cancellationToken)).Any())
        {
            return new ServiceResponse<CompanyDto>
            {
                Errors = ["At least one active item mapping is required before item mapping can be enabled."],
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var companyToUpdate = _mapper.MapToEntity(companyDto);
        companyToUpdate.ItemMappingEnabled = companyDto.ItemMappingEnabled ?? existing.ItemMappingEnabled;
        var company = await _companyRepository.UpdateCompanyAsync(companyToUpdate, cancellationToken);
        
        _dbTransaction.Commit();
        
        return new ServiceResponse<CompanyDto>
        {
            Data = _mapper.MapToDto(company),
            Message = "Company updated successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    private async Task<bool> IsSelectableBusinessUnitAsync(int? businessUnitId,
        CancellationToken cancellationToken)
    {
        if (!businessUnitId.HasValue || _businessUnitRepository is null)
            return true;

        var businessUnit = await _businessUnitRepository.GetAsync(businessUnitId.Value, cancellationToken);
        return businessUnit is { IsActive: true };
    }

    private static ServiceResponse<CompanyDto> InvalidBusinessUnitResponse() => new()
    {
        Errors = ["Select an active Business Unit."],
        StatusCode = StatusCodes.Status400BadRequest
    };
}
