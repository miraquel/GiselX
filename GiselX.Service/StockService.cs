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

public class StockService : IStockService
{
    private readonly IStockRepository _stockRepository;
    private readonly IDbTransaction _dbTransaction;
    private readonly UserClaimDto _userClaimDto;
    private readonly MapperlyMapper _mapper = new();
    private readonly UserManager<AppIdentityUser> _userManager;

    public StockService(IStockRepository stockRepository, IDbTransaction dbTransaction,
        UserClaimDto userClaimDto, UserManager<AppIdentityUser> userManager)
    {
        _stockRepository = stockRepository;
        _dbTransaction = dbTransaction;
        _userClaimDto = userClaimDto;
        _userManager = userManager;
    }

    public async Task<ServiceResponse> UploadAsync(IEnumerable<StockDto> stocks, int companyId,
        CancellationToken cancellationToken)
    {
        var dtos = stocks as StockDto[] ?? stocks.ToArray();

        if (dtos.Length == 0)
        {
            throw new ArgumentException("No stocks to upload.", nameof(stocks));
        }

        dtos = dtos.Where(s => !string.IsNullOrWhiteSpace(s.ProductId)).ToArray();

        var errors = new Dictionary<int, List<string>>();

        foreach (var item in dtos.Select((value, index) => new { Value = value, Index = index }))
        {
            var rowErrors = new List<string>();

            if (string.IsNullOrWhiteSpace(item.Value.ProductId))
            {
                rowErrors.Add("ProductId is required.");
                errors.Add(item.Index + 1, rowErrors);
                continue;
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

        await _stockRepository.UploadAsync(_mapper.MapToEntity(dtos), cancellationToken);

        _dbTransaction.Commit();

        return new ServiceResponse
        {
            Message = $"{dtos.Length} stocks uploaded successfully."
        };
    }

    public async Task<ServiceResponse<PagedListDto<StockDto>>> SelectAsync(PagedListRequestDto pagedListRequest,
        CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(_userClaimDto.Username)
            ?? throw new InvalidOperationException("User not found.");

        var pagedList = await _stockRepository.SelectAsync(
            _mapper.MapToEntity(pagedListRequest), user.CompanyId, cancellationToken);

        return new ServiceResponse<PagedListDto<StockDto>>
        {
            Data = _mapper.MapToDto(pagedList),
            Message = "Stocks retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    public async Task<ServiceResponse<IEnumerable<StockDto>>> SelectByCustPeriodAsync(int companyId, int year,
        int month, CancellationToken cancellationToken)
    {
        var results = await _stockRepository.SelectByCustPeriodAsync(companyId, year, month, cancellationToken);

        return new ServiceResponse<IEnumerable<StockDto>>
        {
            Data = _mapper.MapToDto(results),
            Message = "Stocks retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    public async Task<ServiceResponse<IEnumerable<PeriodDto>>> SelectPeriodsAsync(CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByNameAsync(_userClaimDto.Username)
            ?? throw new InvalidOperationException("User not found.");

        var periods = await _stockRepository.SelectPeriodsAsync(user.CompanyId, cancellationToken);

        return new ServiceResponse<IEnumerable<PeriodDto>>
        {
            Data = _mapper.MapToDto(periods),
            Message = "Periods retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }
}
