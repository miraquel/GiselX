using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;

namespace GiselX.Service;

public sealed class BusinessUnitService(IBusinessUnitRepository repository, IDbTransaction transaction)
    : IBusinessUnitService
{
    public async Task<ServiceResponse<IReadOnlyList<BusinessUnitDto>>> ListAsync(bool activeOnly,
        CancellationToken token) => new()
    {
        Data = (await repository.ListAsync(activeOnly, token)).Select(ToDto).ToList(),
        Message = "Business units retrieved successfully."
    };

    public async Task<ServiceResponse<BusinessUnitDto?>> GetAsync(int id, CancellationToken token)
    {
        var value = await repository.GetAsync(id, token);
        return new ServiceResponse<BusinessUnitDto?>
        {
            Data = value is null ? null : ToDto(value),
            StatusCode = value is null ? StatusCodes.Status404NotFound : StatusCodes.Status200OK,
            Errors = value is null ? ["The business unit was not found."] : []
        };
    }

    public async Task<ServiceResponse<int>> SaveAsync(BusinessUnitDto dto, string username,
        CancellationToken token)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > 100)
            return Invalid(name.Length == 0 ? "Business Unit Name is required." :
                "Business Unit Name must not exceed 100 characters.");

        // Naming the bad entries beats a generic "invalid email": these lists are pasted in by hand
        // and a single stray character in a five-address list is otherwise a hunt.
        var invalidEmails = NotificationRecipients.FindInvalid(dto.NotificationEmails);
        if (invalidEmails.Count > 0)
            return Invalid($"These are not valid email addresses: {string.Join(", ", invalidEmails)}. " +
                           "Separate multiple addresses with ';'.");

        var notificationEmails = NotificationRecipients.Normalize(dto.NotificationEmails);
        if (notificationEmails is { Length: > NotificationRecipients.MaxLength })
            return Invalid($"The notification email list must not exceed {NotificationRecipients.MaxLength} characters.");

        var duplicate = (await repository.ListAsync(false, token)).Any(value => value.Id != dto.Id &&
            string.Equals(value.Name.Trim(), name, StringComparison.OrdinalIgnoreCase));
        if (duplicate) return Invalid("A business unit with this name already exists.");

        var now = DateTime.UtcNow;
        try
        {
            if (dto.Id > 0)
            {
                var current = await repository.GetAsync(dto.Id, token);
                if (current is null)
                    return new ServiceResponse<int>
                    {
                        StatusCode = StatusCodes.Status404NotFound,
                        Errors = ["The business unit was not found."]
                    };
                current.Name = name;
                current.NotificationEmails = notificationEmails;
                current.IsActive = dto.IsActive;
                current.ModifiedBy = username;
                current.ModifiedDate = now;
                await repository.UpdateAsync(current, token);
                transaction.Commit();
                return new ServiceResponse<int> { Data = current.Id, Message = "Business unit saved successfully." };
            }

            var id = await repository.InsertAsync(new BusinessUnit
            {
                Name = name, NotificationEmails = notificationEmails, IsActive = dto.IsActive,
                CreatedBy = username, CreatedDate = now
            }, token);
            transaction.Commit();
            return new ServiceResponse<int> { Data = id, Message = "Business unit saved successfully." };
        }
        catch (BusinessUnitDuplicateException)
        {
            return Invalid("A business unit with this name already exists.");
        }
    }

    public async Task<ServiceResponse> SetActiveAsync(int id, bool active, string username, CancellationToken token)
    {
        if (await repository.GetAsync(id, token) is null)
            return new ServiceResponse
            {
                StatusCode = StatusCodes.Status404NotFound,
                Errors = ["The business unit was not found."]
            };
        await repository.SetActiveAsync(id, active, username, DateTime.UtcNow, token);
        transaction.Commit();
        return new ServiceResponse { Message = active ? "Business unit activated." : "Business unit deactivated." };
    }

    public async Task<ServiceResponse> DeleteAsync(int id, CancellationToken token)
    {
        var result = await repository.DeleteAsync(id, token);
        if (result == BusinessUnitDeleteResult.NotFound)
            return new ServiceResponse { StatusCode = 404, Errors = ["The business unit was not found."] };
        if (result == BusinessUnitDeleteResult.InUse)
            return new ServiceResponse
            {
                StatusCode = 409,
                Errors = ["This business unit is assigned to a company and cannot be deleted. Deactivate it instead."]
            };
        transaction.Commit();
        return new ServiceResponse { Message = "Business unit deleted successfully." };
    }

    private static ServiceResponse<int> Invalid(string error) => new()
    {
        StatusCode = StatusCodes.Status400BadRequest,
        Message = "The business unit is invalid.",
        Errors = [error]
    };

    private static BusinessUnitDto ToDto(BusinessUnit value) => new()
    {
        Id = value.Id,
        Name = value.Name,
        NotificationEmails = value.NotificationEmails,
        IsActive = value.IsActive
    };
}
