using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace GiselX.Service;

public sealed class PowerBiReportService(
    IPowerBiReportRepository repository,
    ICompanyRepository companyRepository,
    IDbTransaction dbTransaction,
    UserClaimDto userClaimDto,
    UserManager<AppIdentityUser> userManager) : IPowerBiReportService
{
    public async Task<ServiceResponse<IReadOnlyList<PowerBiReportDto>>> ListAdminAsync(int? companyId,
        CancellationToken token)
    {
        if (companyId <= 0)
            return Invalid<IReadOnlyList<PowerBiReportDto>>("Company is required.");

        var reports = await repository.ListAdminAsync(companyId, token);
        var companyNames = (await companyRepository.GetAllAsync(token)).ToDictionary(company => company.Id,
            company => company.Name);
        return new ServiceResponse<IReadOnlyList<PowerBiReportDto>>
        {
            Data = reports.Select(report => ToDto(report, companyNames.GetValueOrDefault(report.CompanyId))).ToList(),
            Message = "Power BI reports retrieved successfully."
        };
    }

    public async Task<ServiceResponse<PowerBiReportDto?>> GetAdminAsync(int id, CancellationToken token)
    {
        var report = await repository.GetAdminByIdAsync(id, token);
        if (report is null)
            return NotFound<PowerBiReportDto?>();

        var companyName = (await companyRepository.GetAllAsync(token))
            .FirstOrDefault(company => company.Id == report.CompanyId)?.Name;
        return new ServiceResponse<PowerBiReportDto?>
        {
            Data = ToDto(report, companyName),
            Message = "Power BI report retrieved successfully."
        };
    }

    public async Task<ServiceResponse<IReadOnlyList<CompanyOptionDto>>> ListCompanyOptionsAsync(CancellationToken token)
    {
        var companies = await companyRepository.GetAllAsync(token);
        return new ServiceResponse<IReadOnlyList<CompanyOptionDto>>
        {
            Data = companies.Select(company => new CompanyOptionDto(company.Id, company.Name)).ToList(),
            Message = "Company options retrieved successfully."
        };
    }

    public async Task<ServiceResponse<int>> GetNextDisplayOrderAsync(int companyId, CancellationToken token)
    {
        if (companyId <= 0)
            return Invalid<int>("Company is required.");
        if (!(await companyRepository.GetAllAsync(token)).Any(company => company.Id == companyId))
            return Invalid<int>("The selected company does not exist.");

        return new ServiceResponse<int>
        {
            Data = await repository.GetNextDisplayOrderAsync(companyId, token),
            Message = "Next display order retrieved successfully."
        };
    }

    public async Task<ServiceResponse<int>> SaveAsync(SavePowerBiReportDto dto, string username,
        CancellationToken token)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        var errors = Validate(dto, name, out var normalizedUrl);
        if (errors.Count > 0)
            return Invalid<int>(errors);

        if (!(await companyRepository.GetAllAsync(token)).Any(company => company.Id == dto.CompanyId))
            return Invalid<int>("The selected company does not exist.");
        if (await repository.NameExistsAsync(dto.CompanyId, name, dto.Id, token))
            return Invalid<int>("A Power BI report with this name already exists for the company.");

        var now = DateTime.UtcNow;
        int id;
        if (dto.Id.HasValue)
        {
            var report = await repository.GetAdminByIdAsync(dto.Id.Value, token);
            if (report is null)
                return NotFound<int>();

            Apply(dto, report, name, normalizedUrl);
            report.ModifiedBy = username;
            report.ModifiedDate = now;
            try
            {
                if (!await repository.UpdateAsync(report, token))
                    return NotFound<int>();
            }
            catch (PowerBiReportDuplicateNameException)
            {
                return DuplicateNameResponse();
            }
            id = report.Id;
        }
        else
        {
            var report = new PowerBiReport
            {
                CompanyId = dto.CompanyId,
                Name = name,
                EmbedUrl = normalizedUrl,
                DisplayOrder = dto.DisplayOrder,
                IsActive = dto.IsActive,
                CreatedBy = username,
                CreatedDate = now
            };
            try
            {
                id = await repository.InsertAsync(report, token);
            }
            catch (PowerBiReportDuplicateNameException)
            {
                return DuplicateNameResponse();
            }
        }

        dbTransaction.Commit();
        return new ServiceResponse<int> { Data = id, Message = "Power BI report saved successfully." };
    }

    public async Task<ServiceResponse> SetActiveAsync(int id, bool isActive, string username,
        CancellationToken token)
    {
        if (!await repository.SetActiveAsync(id, isActive, username, DateTime.UtcNow, token))
            return NotFound();

        dbTransaction.Commit();
        return new ServiceResponse
        {
            Message = isActive ? "Power BI report activated." : "Power BI report deactivated."
        };
    }

    public async Task<ServiceResponse> DeleteAsync(int id, CancellationToken token)
    {
        if (!await repository.DeleteAsync(id, token))
            return NotFound();

        dbTransaction.Commit();
        return new ServiceResponse { Message = "Power BI report deleted successfully." };
    }

    public async Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>> ListMenuAsync(CancellationToken token)
    {
        var reports = await repository.ListActiveByCompanyAsync(await CurrentCompanyIdAsync(), token);
        return new ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>
        {
            Data = reports.Select(report => new PowerBiReportMenuItemDto(report.Id, report.Name)).ToList(),
            Message = "Power BI report menu retrieved successfully."
        };
    }

    public async Task<ServiceResponse<PowerBiReportViewerDto?>> GetViewerAsync(int id, CancellationToken token)
    {
        var report = await repository.GetActiveByIdAndCompanyAsync(id, await CurrentCompanyIdAsync(), token);
        if (report is null)
            return new ServiceResponse<PowerBiReportViewerDto?>
            {
                StatusCode = StatusCodes.Status404NotFound,
                Errors = ["The Power BI report was not found."]
            };

        return new ServiceResponse<PowerBiReportViewerDto?>
        {
            Data = new PowerBiReportViewerDto(report.Id, report.Name, report.EmbedUrl),
            Message = "Power BI report retrieved successfully."
        };
    }

    private async Task<int> CurrentCompanyIdAsync()
    {
        var user = await userManager.FindByNameAsync(userClaimDto.Username)
            ?? throw new InvalidOperationException("User not found.");
        return user.CompanyId;
    }

    private static List<string> Validate(SavePowerBiReportDto dto, string name, out string normalizedUrl)
    {
        var errors = new List<string>();
        if (dto.CompanyId <= 0) errors.Add("Company is required.");
        if (string.IsNullOrEmpty(name)) errors.Add("Report name is required.");
        if (name.Length > 150) errors.Add("Report name must not exceed 150 characters.");
        if (dto.DisplayOrder < 0) errors.Add("Display order must be non-negative.");

        var candidateUrl = dto.EmbedUrl?.Trim();
        if (candidateUrl?.Length > 2048)
        {
            normalizedUrl = string.Empty;
            errors.Add("Report URL must not exceed 2,048 characters.");
        }
        else if (!PowerBiPublishUrlValidator.TryNormalize(candidateUrl, out normalizedUrl, out var urlError))
        {
            errors.Add(urlError);
        }
        else if (normalizedUrl.Length > 2048)
        {
            errors.Add("Report URL must not exceed 2,048 characters after normalization.");
        }

        return errors;
    }

    private static void Apply(SavePowerBiReportDto dto, PowerBiReport report, string name, string embedUrl)
    {
        report.CompanyId = dto.CompanyId;
        report.Name = name;
        report.EmbedUrl = embedUrl;
        report.DisplayOrder = dto.DisplayOrder;
        report.IsActive = dto.IsActive;
    }

    private static PowerBiReportDto ToDto(PowerBiReport report, string? companyName) => new()
    {
        Id = report.Id,
        CompanyId = report.CompanyId,
        CompanyName = companyName ?? string.Empty,
        Name = report.Name,
        EmbedUrl = report.EmbedUrl,
        DisplayOrder = report.DisplayOrder,
        IsActive = report.IsActive,
        CreatedDate = report.CreatedDate,
        CreatedBy = report.CreatedBy,
        ModifiedDate = report.ModifiedDate,
        ModifiedBy = report.ModifiedBy
    };

    private static ServiceResponse<T> Invalid<T>(params string[] errors) => Invalid<T>((IEnumerable<string>)errors);

    private static ServiceResponse<int> DuplicateNameResponse() =>
        Invalid<int>("A Power BI report with this name already exists for the company.");

    private static ServiceResponse<T> Invalid<T>(IEnumerable<string> errors) => new()
    {
        Errors = errors.ToList(),
        Message = "The Power BI report is invalid.",
        StatusCode = StatusCodes.Status400BadRequest
    };

    private static ServiceResponse NotFound() => new()
    {
        Errors = ["The Power BI report was not found."],
        StatusCode = StatusCodes.Status404NotFound
    };

    private static ServiceResponse<T> NotFound<T>() => new()
    {
        Errors = ["The Power BI report was not found."],
        StatusCode = StatusCodes.Status404NotFound
    };
}
