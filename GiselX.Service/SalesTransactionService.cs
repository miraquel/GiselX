using System.Data;
using GiselX.Domain;
using GiselX.Mapper;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace GiselX.Service;

public class SalesTransactionService : ISalesTransactionService
{
    private readonly ISalesTransactionRepository _salesTransactionRepository;
    private readonly IDbTransaction _dbTransaction;
    private readonly UserClaimDto _userClaimDto;
    private readonly MapperlyMapper _mapper = new();
    private readonly UserManager<AppIdentityUser> _userManager;

    public SalesTransactionService(ISalesTransactionRepository salesTransactionRepository, IDbTransaction dbTransaction,
        UserClaimDto userClaimDto, UserManager<AppIdentityUser> userManager)
    {
        _salesTransactionRepository = salesTransactionRepository;
        _dbTransaction = dbTransaction;
        _userClaimDto = userClaimDto;
        _userManager = userManager;
    }

    public async Task<ServiceResponse> UploadAsync(IEnumerable<SalesTransactionDto> salesTransactions, int companyId,
        CancellationToken cancellationToken)
    {
        var dtos = salesTransactions as SalesTransactionDto[] ?? salesTransactions.ToArray();

        if (dtos.Length == 0)
        {
            throw new ArgumentException("No sales transactions to upload.", nameof(salesTransactions));
        }

        dtos = dtos.Where(t => !string.IsNullOrWhiteSpace(t.InvoiceNo) && !string.IsNullOrWhiteSpace(t.ProductId)).ToArray();

        var errors = new Dictionary<int, List<string>>();

        foreach (var item in dtos.Select((value, index) => new { Value = value, Index = index }))
        {
            var rowErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(item.Value.InvoiceNo))
            {
                rowErrors.Add("InvoiceNo is required.");
            }

            if (string.IsNullOrWhiteSpace(item.Value.ProductId))
            {
                rowErrors.Add("ProductId is required.");
            }

            if (string.IsNullOrWhiteSpace(item.Value.InvoiceNo) || string.IsNullOrWhiteSpace(item.Value.ProductId))
            {
                errors.Add(item.Index + 1, rowErrors);
                continue;
            }

            if (item.Value.InvoiceDate == DateTime.MinValue)
            {
                rowErrors.Add($"InvoiceDate is required for InvoiceNo {item.Value.InvoiceNo}.");
            }

            if (item.Value.InvoiceQty <= 0)
            {
                rowErrors.Add($"InvoiceQty must be greater than zero for InvoiceNo {item.Value.InvoiceNo}.");
            }

            if (rowErrors.Count != 0)
            {
                errors.Add(item.Index + 1, rowErrors);
            }
        }

        var allErrorMessages = string.Empty;
        foreach (var error in errors)
        {
            var errorString = string.Join("; ", error.Value);
            if (!string.IsNullOrWhiteSpace(errorString))
            {
                allErrorMessages += $"Row {error.Key}: {errorString}\n";
            }
        }

        if (!string.IsNullOrWhiteSpace(allErrorMessages))
        {
            return new ServiceResponse
            {
                Errors = errors.SelectMany(e => e.Value).ToList(),
                Message = $"Validation failed for {errors.Count} rows.\n{allErrorMessages}",
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        await _salesTransactionRepository.UploadAsync(_mapper.MapToEntity(dtos), cancellationToken);

        _dbTransaction.Commit();

        return new ServiceResponse
        {
            Message = $"{dtos.Length} sales transactions uploaded successfully."
        };
    }

    public async Task<ServiceResponse<PagedListDto<SalesTransactionDto>>> SelectAsync(PagedListRequestDto pagedListRequest,
        bool useDistributorIdentity, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(_userClaimDto.Username)
            ?? throw new InvalidOperationException("User not found.");
        var companyId = _userClaimDto.IsAdministrator ? 0 : user.CompanyId;

        var pagedList = await _salesTransactionRepository.SelectAsync(
            _mapper.MapToEntity(pagedListRequest), companyId, useDistributorIdentity, cancellationToken);

        return new ServiceResponse<PagedListDto<SalesTransactionDto>>
        {
            Data = _mapper.MapToDto(pagedList),
            Message = "Sales transactions retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    public async Task<ServiceResponse<IEnumerable<SalesTransactionDto>>> SelectByCustPeriodAsync(int companyId, int year,
        int month, bool useDistributorIdentity, CancellationToken cancellationToken)
    {
        var scopeCompanyId = await ResolveCompanyScopeAsync();
        var results = await _salesTransactionRepository.SelectByCustPeriodAsync(scopeCompanyId, year, month,
            useDistributorIdentity, cancellationToken);

        return new ServiceResponse<IEnumerable<SalesTransactionDto>>
        {
            Data = _mapper.MapToDto(results),
            Message = "Sales transactions retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    public async Task<ServiceResponse<IEnumerable<PeriodDto>>> SelectPeriodsAsync(CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(_userClaimDto.Username)
            ?? throw new InvalidOperationException("User not found.");

        var companyId = _userClaimDto.IsAdministrator ? 0 : user.CompanyId;
        var periods = await _salesTransactionRepository.SelectPeriodsAsync(companyId, cancellationToken);

        return new ServiceResponse<IEnumerable<PeriodDto>>
        {
            Data = _mapper.MapToDto(periods),
            Message = "Periods retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    public async Task<ServiceResponse<SalesSelectionSummaryDto>> SelectSummaryAsync(PagedListRequestDto request,
        bool useDistributorIdentity, CancellationToken cancellationToken)
    {
        var companyId = await ResolveCompanyScopeAsync();
        var summary = await _salesTransactionRepository.SummarizeAsync(
            _mapper.MapToEntity(request), companyId, useDistributorIdentity, cancellationToken);
        return new ServiceResponse<SalesSelectionSummaryDto>
        {
            Data = new SalesSelectionSummaryDto
            {
                DistributorCount = summary.DistributorCount, RowCount = summary.RowCount,
                InvoiceQty = summary.InvoiceQty, GrossValue = summary.GrossValue,
                DiscountValue = summary.DiscountValue, NetValue = summary.NetValue
            },
            Message = "Sellout summary retrieved successfully."
        };
    }

    private async Task<int> ResolveCompanyScopeAsync()
    {
        if (_userClaimDto.IsAdministrator) return 0;

        var user = await _userManager.FindByNameAsync(_userClaimDto.Username)
            ?? throw new InvalidOperationException("User not found.");
        return user.CompanyId;
    }
}
