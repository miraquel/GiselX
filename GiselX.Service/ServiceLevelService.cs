using System.Data;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;

namespace GiselX.Service;

public class ServiceLevelService : IServiceLevelService
{
    private readonly IServiceLevelRepository _serviceLevelRepository;
    private readonly IDbTransaction _dbTransaction;
    private readonly UserClaimDto _userClaimDto;
    private readonly MapperlyMapper _mapper = new();

    public ServiceLevelService(IServiceLevelRepository serviceLevelRepository, IDbTransaction dbTransaction, UserClaimDto userClaimDto)
    {
        _serviceLevelRepository = serviceLevelRepository;
        _dbTransaction = dbTransaction;
        _userClaimDto = userClaimDto;
    }

    public async Task<ServiceResponse> UploadAsync(IEnumerable<ServiceLevelDto> serviceLevels, CancellationToken cancellationToken)
    {
       var transDistDtos = serviceLevels as ServiceLevelDto[] ?? serviceLevels.ToArray();
        
        var errors = new Dictionary<int, List<string>>();
         
        if (serviceLevels == null || transDistDtos.Length == 0)
        {
            throw new ArgumentException("No transaction distributions to upload.", nameof(serviceLevels));
        }
        
        transDistDtos = transDistDtos.Where(t => !string.IsNullOrWhiteSpace(t.SoId) && !string.IsNullOrWhiteSpace(t.ItemId)).ToArray();
        
        // Validate each transaction distribution
        foreach (var transDistDto in transDistDtos.Select((value, index) => new { Value = value, Index = index }))
        {
            var rowErrors = new List<string>();
            
            if (string.IsNullOrWhiteSpace(transDistDto.Value.SoId))
            {
                rowErrors.Add("SoId is required.");
            }

            if (string.IsNullOrWhiteSpace(transDistDto.Value.ItemId))
            {
                rowErrors.Add("ItemId is required.");
            }
            
            if (string.IsNullOrWhiteSpace(transDistDto.Value.SoId) || string.IsNullOrWhiteSpace(transDistDto.Value.ItemId))
            {
                // Skip further validation if SoId or ItemId is missing
                errors.Add(transDistDto.Index + 1, rowErrors);
                continue;
            }

            if (transDistDto.Value.SoCreateDate == DateTime.MinValue)
            {
                rowErrors.Add($"SoCreateDate is required for SoId {transDistDto.Value.SoId}.");
            }

            if (transDistDto.Value.DlvDateRequest == DateTime.MinValue)
            {
                rowErrors.Add($"DlvDateRequest is required for SoId {transDistDto.Value.SoId}.");
            }

            if (transDistDto.Value.RctDateRequest == DateTime.MinValue)
            {
                rowErrors.Add($"RctDateRequest is required for SoId {transDistDto.Value.SoId}.");
            }

            if (transDistDto.Value.SoQty <= 0)
            {
                rowErrors.Add($"SoQty must be greater than zero for SoId {transDistDto.Value.SoId}.");
            }

            if (rowErrors.Count != 0)
            {
                errors.Add(transDistDto.Index + 1, rowErrors);
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

        var originalTransDists = await _serviceLevelRepository.SelectByCustAsync(_userClaimDto.Username, cancellationToken);
        
        var existingTransDists = transDistDtos.Where(td => originalTransDists.Any(otd => otd.SoId == td.SoId && otd.ItemId == td.ItemId)).ToArray();
        
        // update existing transaction distributions
        foreach (var existingTransDist in existingTransDists)
        {
            await _serviceLevelRepository.DeleteAsync(_mapper.MapToEntity(existingTransDist), cancellationToken);
        }

        await _serviceLevelRepository.UploadAsync(_mapper.MapToEntity(transDistDtos), cancellationToken);
        
        // Commit the transaction
        _dbTransaction.Commit();
        
        return new ServiceResponse
        {
            Message = $"{transDistDtos.Length} transaction distributions uploaded successfully."
        };
    }

    public async Task<ServiceResponse<PagedListDto<ServiceLevelDto>>> SelectAsync(PagedListRequestDto pagedListRequest, CancellationToken cancellationToken)
    {
        var pagedList = await _serviceLevelRepository.SelectAsync(_mapper.MapToEntity(pagedListRequest), _userClaimDto.Username, cancellationToken);
        
        return new ServiceResponse<PagedListDto<ServiceLevelDto>>
        {
            Data = _mapper.MapToDto(pagedList),
            Message = "Transaction distributions retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    public async Task<ServiceResponse<IEnumerable<ServiceLevelDto>>> SelectByCustPeriodAsync(string custCode, int year, int month, CancellationToken cancellationToken)
    {
        var serviceLevels = await _serviceLevelRepository.SelectByCustPeriodAsync(custCode, year, month, cancellationToken);
        
        return new ServiceResponse<IEnumerable<ServiceLevelDto>>
        {
            Data = _mapper.MapToDto(serviceLevels),
            Message = "Transaction distributions retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    public async Task<ServiceResponse<IEnumerable<PeriodDto>>> SelectPeriodsAsync(CancellationToken cancellationToken)
    {
        var periods = await _serviceLevelRepository.SelectPeriodsAsync(_userClaimDto.Username, cancellationToken);
        
        return new ServiceResponse<IEnumerable<PeriodDto>>
        {
            Data = _mapper.MapToDto(periods),
            Message = "Periods retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }
}