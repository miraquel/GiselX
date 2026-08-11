using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;

namespace GiselX.Service;

public sealed class ItemMappingService(IItemMappingRepository repository, IDbTransaction dbTransaction)
    : IItemMappingService
{
    public async Task<ServiceResponse<IReadOnlyList<ItemMappingDto>>> ListAsync(int companyId, CancellationToken token)
    {
        var mappings = await repository.GetByCompanyAsync(companyId, token);
        return new ServiceResponse<IReadOnlyList<ItemMappingDto>>
        {
            Data = mappings.Select(ToDto).ToList(),
            Message = "Item mappings retrieved successfully."
        };
    }

    public async Task<ServiceResponse<ItemMappingDto?>> GetAsync(int id, int companyId, CancellationToken token)
    {
        var mapping = await repository.GetByIdAsync(id, companyId, token);
        return new ServiceResponse<ItemMappingDto?>
        {
            Data = mapping is null ? null : ToDto(mapping),
            StatusCode = mapping is null ? StatusCodes.Status404NotFound : StatusCodes.Status200OK,
            Errors = mapping is null ? ["The item mapping was not found."] : []
        };
    }

    public async Task<ServiceResponse<int>> SaveAsync(SaveItemMappingDto dto, string username, CancellationToken token)
    {
        var existingMappings = await repository.GetByCompanyAsync(dto.CompanyId, token);
        var errors = Validate(dto, existingMappings);
        if (errors.Count > 0)
        {
            return new ServiceResponse<int>
            {
                Errors = errors,
                Message = "The item mapping is invalid.",
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var now = DateTime.UtcNow;
        int id;
        if (dto.Id.HasValue)
        {
            var current = await repository.GetByIdAsync(dto.Id.Value, dto.CompanyId, token);
            if (current is null)
            {
                return new ServiceResponse<int>
                {
                    Errors = ["The item mapping was not found."],
                    StatusCode = StatusCodes.Status404NotFound
                };
            }

            Apply(dto, current);
            current.ModifiedBy = username;
            current.ModifiedDate = now;
            try
            {
                await repository.UpdateAsync(current, token);
            }
            catch (ItemMappingDuplicateException)
            {
                return DuplicateDistributorItemIdResponse();
            }
            id = current.Id;
        }
        else
        {
            var mapping = new ItemMapping
            {
                CompanyId = dto.CompanyId,
                GmkItemId = dto.GmkItemId.Trim(),
                GmkItemName = dto.GmkItemName.Trim(),
                DistributorItemId = dto.DistributorItemId.Trim(),
                DistributorItemName = dto.DistributorItemName.Trim(),
                IsActive = true,
                CreatedBy = username,
                CreatedDate = now
            };
            try
            {
                id = await repository.InsertAsync(mapping, token);
            }
            catch (ItemMappingDuplicateException)
            {
                return DuplicateDistributorItemIdResponse();
            }
        }

        dbTransaction.Commit();
        return new ServiceResponse<int> { Data = id, Message = "Item mapping saved successfully." };
    }

    public async Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool active, string username,
        CancellationToken token)
    {
        if (await repository.GetByIdAsync(id, companyId, token) is null)
        {
            return new ServiceResponse
            {
                Errors = ["The item mapping was not found."],
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        await repository.SetActiveAsync(id, companyId, active, username, DateTime.UtcNow, token);
        dbTransaction.Commit();
        return new ServiceResponse { Message = active ? "Mapping activated." : "Mapping deactivated." };
    }

    public async Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken token)
    {
        var result = await repository.DeleteAsync(id, companyId, token);
        if (result == ItemMappingDeleteResult.NotFound)
            return new ServiceResponse
            {
                StatusCode = StatusCodes.Status404NotFound,
                Errors = ["The item mapping was not found."]
            };
        if (result == ItemMappingDeleteResult.HasUploadHistory)
            return new ServiceResponse
            {
                StatusCode = StatusCodes.Status409Conflict,
                Errors = ["This mapping has upload history and cannot be deleted. Deactivate it instead."]
            };

        dbTransaction.Commit();
        return new ServiceResponse { Message = "Item mapping deleted successfully." };
    }

    private static List<string> Validate(SaveItemMappingDto dto, IReadOnlyList<ItemMapping> existingMappings)
    {
        var errors = new List<string>();
        if (dto.CompanyId <= 0) errors.Add("Company is required.");
        if (string.IsNullOrWhiteSpace(dto.GmkItemId)) errors.Add("GMK Item ID is required.");
        if (string.IsNullOrWhiteSpace(dto.GmkItemName)) errors.Add("GMK Item Name is required.");
        if (string.IsNullOrWhiteSpace(dto.DistributorItemId)) errors.Add("Distributor Item ID is required.");
        if (string.IsNullOrWhiteSpace(dto.DistributorItemName)) errors.Add("Distributor Item Name is required.");
        if (dto.GmkItemId?.Trim().Length > 100) errors.Add("GMK Item ID must not exceed 100 characters.");
        if (dto.GmkItemName?.Trim().Length > 200) errors.Add("GMK Item Name must not exceed 200 characters.");
        if (dto.DistributorItemId?.Trim().Length > 100) errors.Add("Distributor Item ID must not exceed 100 characters.");
        if (dto.DistributorItemName?.Trim().Length > 200) errors.Add("Distributor Item Name must not exceed 200 characters.");
        var distributorItemId = dto.DistributorItemId?.Trim();
        if (!string.IsNullOrEmpty(distributorItemId) && existingMappings.Any(mapping => mapping.Id != dto.Id &&
            string.Equals(mapping.DistributorItemId?.Trim(), distributorItemId, StringComparison.OrdinalIgnoreCase)))
            errors.Add("A mapping with this Distributor Item ID already exists for the company.");
        return errors;
    }

    private static ServiceResponse<int> DuplicateDistributorItemIdResponse() => new()
    {
        Errors = ["A mapping with this Distributor Item ID already exists for the company."],
        Message = "The item mapping is invalid.",
        StatusCode = StatusCodes.Status400BadRequest
    };

    private static void Apply(SaveItemMappingDto dto, ItemMapping mapping)
    {
        mapping.GmkItemId = dto.GmkItemId.Trim();
        mapping.GmkItemName = dto.GmkItemName.Trim();
        mapping.DistributorItemId = dto.DistributorItemId.Trim();
        mapping.DistributorItemName = dto.DistributorItemName.Trim();
    }

    private static ItemMappingDto ToDto(ItemMapping mapping) => new()
    {
        Id = mapping.Id,
        CompanyId = mapping.CompanyId,
        GmkItemId = mapping.GmkItemId,
        GmkItemName = mapping.GmkItemName,
        DistributorItemId = mapping.DistributorItemId,
        DistributorItemName = mapping.DistributorItemName,
        IsActive = mapping.IsActive,
        CreatedDate = mapping.CreatedDate,
        CreatedBy = mapping.CreatedBy,
        ModifiedDate = mapping.ModifiedDate,
        ModifiedBy = mapping.ModifiedBy
    };
}
