using System.Data;
using System.Globalization;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;

namespace GiselX.Service;

public sealed class SalesmanService(ISalesmanRepository repository, IDbTransaction transaction)
    : ISalesmanService
{
    public async Task<ServiceResponse<IReadOnlyList<SalesmanDto>>> ListAsync(bool activeOnly,
        CancellationToken token) => new()
    {
        Data = (await repository.ListAsync(activeOnly, token)).Select(ToDto).ToList(),
        Message = "Salesmen retrieved successfully."
    };

    public async Task<ServiceResponse<string>> GetNextCodeAsync(CancellationToken token) => new()
    {
        Data = NextCode(await repository.ListAsync(false, token)),
        Message = "Next salesman code generated."
    };

    public async Task<ServiceResponse<SalesmanDto?>> GetAsync(int id, CancellationToken token)
    {
        var value = await repository.GetAsync(id, token);
        return new ServiceResponse<SalesmanDto?>
        {
            Data = value is null ? null : ToDto(value),
            StatusCode = value is null ? StatusCodes.Status404NotFound : StatusCodes.Status200OK,
            Errors = value is null ? [NotFound] : []
        };
    }

    public async Task<ServiceResponse<int>> SaveAsync(SalesmanDto dto, string username, CancellationToken token)
    {
        var existing = await repository.ListAsync(false, token);
        var code = dto.Code?.Trim() ?? string.Empty;
        var name = dto.Name?.Trim() ?? string.Empty;
        // A blank code on create means "let the system number it"; on edit the code must stay explicit.
        if (code.Length == 0 && dto.Id == 0) code = NextCode(existing);

        var errors = Validate(code, name);
        // Named individually: these lists are pasted in by hand, and "invalid email" in a five-address
        // list is a hunt. Same rule the Company and Business Unit screens use.
        var invalidEmails = NotificationRecipients.FindInvalid(dto.NotificationEmails);
        if (invalidEmails.Count > 0)
            errors.Add($"These are not valid email addresses: {string.Join(", ", invalidEmails)}. " +
                       "Separate multiple addresses with ';'.");
        if (errors.Count > 0) return Invalid(errors);

        var notificationEmails = NotificationRecipients.Normalize(dto.NotificationEmails);

        var duplicate = existing.Any(value =>
            value.Id != dto.Id && string.Equals(value.Code.Trim(), code, StringComparison.OrdinalIgnoreCase));
        if (duplicate) return Invalid([Duplicate]);

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
                        Errors = [NotFound]
                    };
                current.Code = code;
                current.Name = name;
                current.NotificationEmails = notificationEmails;
                current.IsActive = dto.IsActive;
                current.ModifiedBy = username;
                current.ModifiedDate = now;
                await repository.UpdateAsync(current, token);
                transaction.Commit();
                return new ServiceResponse<int> { Data = current.Id, Message = "Salesman saved successfully." };
            }

            var id = await repository.InsertAsync(new Salesman
            {
                Code = code, Name = name, NotificationEmails = notificationEmails,
                IsActive = dto.IsActive, CreatedBy = username, CreatedDate = now
            }, token);
            transaction.Commit();
            return new ServiceResponse<int> { Data = id, Message = "Salesman saved successfully." };
        }
        catch (SalesmanDuplicateException)
        {
            return Invalid([Duplicate]);
        }
    }

    public async Task<ServiceResponse> SetActiveAsync(int id, bool active, string username, CancellationToken token)
    {
        if (await repository.GetAsync(id, token) is null)
            return new ServiceResponse { StatusCode = StatusCodes.Status404NotFound, Errors = [NotFound] };
        await repository.SetActiveAsync(id, active, username, DateTime.UtcNow, token);
        transaction.Commit();
        return new ServiceResponse { Message = active ? "Salesman activated." : "Salesman deactivated." };
    }

    public async Task<ServiceResponse> DeleteAsync(int id, CancellationToken token)
    {
        var result = await repository.DeleteAsync(id, token);
        if (result == SalesmanDeleteResult.NotFound)
            return new ServiceResponse { StatusCode = StatusCodes.Status404NotFound, Errors = [NotFound] };
        if (result == SalesmanDeleteResult.InUse)
            return new ServiceResponse
            {
                StatusCode = StatusCodes.Status409Conflict,
                Errors = ["This salesman is assigned to a company and cannot be deleted. Deactivate it instead."]
            };

        transaction.Commit();
        return new ServiceResponse { Message = "Salesman deleted successfully." };
    }

    private const string NotFound = "The salesman was not found.";
    private const string Duplicate = "A salesman with this code already exists.";

    /// <summary>
    /// The next plain row number, ignoring any code an administrator has overridden with free text
    /// so a manual code never stalls the sequence.
    /// </summary>
    private static string NextCode(IReadOnlyList<Salesman> existing)
    {
        var highest = existing
            .Select(value => int.TryParse(value.Code?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture,
                out var number) ? number : 0)
            .DefaultIfEmpty(0)
            .Max();
        return (highest + 1).ToString(CultureInfo.InvariantCulture);
    }

    private static List<string> Validate(string code, string name)
    {
        var errors = new List<string>();
        if (code.Length == 0) errors.Add("Salesman Code is required.");
        else if (code.Length > 50) errors.Add("Salesman Code must not exceed 50 characters.");
        if (name.Length == 0) errors.Add("Salesman Name is required.");
        else if (name.Length > 200) errors.Add("Salesman Name must not exceed 200 characters.");
        return errors;
    }

    private static ServiceResponse<int> Invalid(List<string> errors) => new()
    {
        StatusCode = StatusCodes.Status400BadRequest,
        Message = "The salesman is invalid.",
        Errors = errors
    };

    private static SalesmanDto ToDto(Salesman value) => new()
        {
            Id = value.Id, Code = value.Code, Name = value.Name,
            NotificationEmails = value.NotificationEmails, IsActive = value.IsActive
        };
}
