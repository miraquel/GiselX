using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;

namespace GiselX.Service;

public sealed class StockAreaService(IStockAreaRepository repository, IDbTransaction transaction)
    : IStockAreaService
{
    private const string NotFound = "The area was not found.";
    private const string DuplicateName = "An area with this name already exists for this company.";

    public async Task<ServiceResponse<IReadOnlyList<StockAreaDto>>> ListAsync(int companyId, CancellationToken token) => new()
    {
        Data = (await repository.ListAsync(companyId, false, token)).Select(ToDto).ToList(),
        Message = "Areas retrieved successfully."
    };

    public async Task<ServiceResponse<StockAreaDto?>> GetAsync(int id, int companyId, CancellationToken token)
    {
        var value = await repository.GetAsync(id, companyId, token);
        return new ServiceResponse<StockAreaDto?>
        {
            Data = value is null ? null : ToDto(value),
            StatusCode = value is null ? StatusCodes.Status404NotFound : StatusCodes.Status200OK,
            Errors = value is null ? [NotFound] : []
        };
    }

    public async Task<ServiceResponse<int>> SaveAsync(StockAreaDto dto, string username, CancellationToken token)
    {
        var name = StockTextNormalizer.Normalize(dto.Name);
        if (name.Length == 0 || name.Length > 100)
            return Invalid(name.Length == 0 ? "Area Name is required." : "Area Name must not exceed 100 characters.");

        var aliasList = StockTextNormalizer.SplitList(dto.Aliases);
        var aliases = aliasList.Count == 0 ? null : string.Join(";", aliasList);
        if (aliases is { Length: > 1000 })
            return Invalid("The aliases must not exceed 1000 characters in total.");

        // Inactive areas are included on purpose: they can be reactivated, and an alias that two
        // areas share would make the area text of a workbook ambiguous the moment that happens.
        var others = (await repository.ListAsync(dto.CompanyId, false, token)).Where(a => a.Id != dto.Id).ToList();
        if (others.Any(a => string.Equals(StockTextNormalizer.Normalize(a.Name), name, StringComparison.OrdinalIgnoreCase)))
            return Invalid(DuplicateName);

        var mine = new[] { name }.Concat(aliasList).ToList();
        foreach (var other in others)
        {
            var theirs = new[] { StockTextNormalizer.Normalize(other.Name) }
                .Concat(StockTextNormalizer.SplitList(other.Aliases)).ToList();
            var clash = mine.FirstOrDefault(text => theirs.Contains(text, StringComparer.OrdinalIgnoreCase));
            if (clash is not null)
                return Invalid($"'{clash}' is already used as a name or alias of the area '{other.Name}'.");
        }

        var now = DateTime.UtcNow;
        try
        {
            if (dto.Id > 0)
            {
                var current = await repository.GetAsync(dto.Id, dto.CompanyId, token);
                if (current is null)
                    return new ServiceResponse<int> { StatusCode = StatusCodes.Status404NotFound, Errors = [NotFound] };
                current.Name = name;
                current.Aliases = aliases;
                current.IsActive = dto.IsActive;
                current.ModifiedBy = username;
                current.ModifiedDate = now;
                await repository.UpdateAsync(current, token);
                transaction.Commit();
                return new ServiceResponse<int> { Data = current.Id, Message = "Area saved successfully." };
            }

            var id = await repository.InsertAsync(new StockArea
            {
                CompanyId = dto.CompanyId, Name = name, Aliases = aliases, IsActive = dto.IsActive,
                CreatedBy = username, CreatedDate = now
            }, token);
            transaction.Commit();
            return new ServiceResponse<int> { Data = id, Message = "Area saved successfully." };
        }
        catch (StockAreaDuplicateException)
        {
            return Invalid(DuplicateName);
        }
    }

    public async Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool active, string username,
        CancellationToken token)
    {
        if (await repository.GetAsync(id, companyId, token) is null)
            return new ServiceResponse { StatusCode = StatusCodes.Status404NotFound, Errors = [NotFound] };
        await repository.SetActiveAsync(id, companyId, active, username, DateTime.UtcNow, token);
        transaction.Commit();
        return new ServiceResponse { Message = active ? "Area activated." : "Area deactivated." };
    }

    public async Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken token)
    {
        if (await repository.GetAsync(id, companyId, token) is null)
            return new ServiceResponse { StatusCode = StatusCodes.Status404NotFound, Errors = [NotFound] };
        if (!await repository.DeleteAsync(id, companyId, token))
            return new ServiceResponse
            {
                StatusCode = StatusCodes.Status409Conflict,
                Errors = ["This area has stock rows and cannot be deleted. Deactivate it instead."]
            };
        transaction.Commit();
        return new ServiceResponse { Message = "Area deleted successfully." };
    }

    private static ServiceResponse<int> Invalid(string error) => new()
    {
        StatusCode = StatusCodes.Status400BadRequest,
        Message = "The area is invalid.",
        Errors = [error]
    };

    private static StockAreaDto ToDto(StockArea value) => new()
    {
        Id = value.Id, CompanyId = value.CompanyId, Name = value.Name, Aliases = value.Aliases,
        IsActive = value.IsActive
    };
}
