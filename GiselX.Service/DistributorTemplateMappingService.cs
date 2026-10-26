using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;

namespace GiselX.Service;

public sealed class DistributorTemplateMappingService : IDistributorTemplateMappingService
{
    private readonly IDistributorTemplateMappingRepository _repository;
    private readonly IDbTransaction _dbTransaction;

    public DistributorTemplateMappingService(IDistributorTemplateMappingRepository repository,
        IDbTransaction dbTransaction)
    {
        _repository = repository;
        _dbTransaction = dbTransaction;
    }

    public async Task<ServiceResponse<IReadOnlyList<DistributorTemplateMappingDto>>> ListAsync(int companyId,
        CancellationToken cancellationToken)
    {
        var mappings = await _repository.GetByCompanyAsync(companyId, cancellationToken);
        return new ServiceResponse<IReadOnlyList<DistributorTemplateMappingDto>>
        {
            Data = mappings.Select(ToDto).ToList(),
            Message = "Distributor template mappings retrieved successfully."
        };
    }

    public async Task<ServiceResponse<DistributorTemplateMappingDto?>> GetAsync(int id, int companyId,
        CancellationToken cancellationToken)
    {
        var mapping = await _repository.GetByIdAsync(id, companyId, cancellationToken);
        return new ServiceResponse<DistributorTemplateMappingDto?>
        {
            Data = mapping is null ? null : ToDto(mapping),
            StatusCode = mapping is null ? StatusCodes.Status404NotFound : StatusCodes.Status200OK,
            Errors = mapping is null ? ["The distributor template mapping was not found."] : []
        };
    }

    public async Task<ServiceResponse<int>> SaveAsync(SaveDistributorTemplateMappingDto dto, string username,
        CancellationToken cancellationToken)
    {
        var existingMappings = await _repository.GetByCompanyAsync(dto.CompanyId, cancellationToken);
        var errors = Validate(dto, existingMappings);
        if (errors.Count > 0)
        {
            return new ServiceResponse<int>
            {
                Errors = errors,
                Message = "The distributor template mapping is invalid.",
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var now = DateTime.UtcNow;
        int id;
        if (dto.Id.HasValue)
        {
            var current = await _repository.GetByIdAsync(dto.Id.Value, dto.CompanyId, cancellationToken);
            if (current is null)
            {
                return new ServiceResponse<int>
                {
                    Errors = ["The distributor template mapping was not found."],
                    StatusCode = StatusCodes.Status404NotFound
                };
            }

            Apply(dto, current);
            current.ModifiedBy = username;
            current.ModifiedDate = now;
            await _repository.UpdateAsync(current, cancellationToken);
            id = current.Id;
        }
        else
        {
            var mapping = new DistributorTemplateMapping
            {
                CompanyId = dto.CompanyId,
                Name = dto.Name.Trim(),
                WorksheetName = dto.WorksheetName.Trim(),
                HeaderRowNumber = dto.HeaderRowNumber,
                FirstDataRowNumber = dto.FirstDataRowNumber,
                HeaderSignature = dto.HeaderSignature,
                IsActive = true,
                CreatedBy = username,
                CreatedDate = now,
                Columns = ToEntities(dto.Columns)
            };
            id = await _repository.InsertAsync(mapping, cancellationToken);
        }

        _dbTransaction.Commit();
        return new ServiceResponse<int>
        {
            Data = id,
            Message = "Distributor template mapping saved successfully."
        };
    }

    public async Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool isActive, string username,
        CancellationToken cancellationToken)
    {
        var mapping = await _repository.GetByIdAsync(id, companyId, cancellationToken);
        if (mapping is null)
        {
            return new ServiceResponse
            {
                Errors = ["The distributor template mapping was not found."],
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        await _repository.SetActiveAsync(id, companyId, isActive, username, DateTime.UtcNow, cancellationToken);
        _dbTransaction.Commit();
        return new ServiceResponse { Message = isActive ? "Mapping activated." : "Mapping deactivated." };
    }

    public async Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken cancellationToken)
    {
        var result = await _repository.DeleteAsync(id, companyId, cancellationToken);
        if (result == DistributorTemplateMappingDeleteResult.NotFound)
        {
            return new ServiceResponse
            {
                StatusCode = StatusCodes.Status404NotFound,
                Errors = ["The distributor template mapping was not found."]
            };
        }

        if (result == DistributorTemplateMappingDeleteResult.HasUploadHistory)
        {
            return new ServiceResponse
            {
                StatusCode = StatusCodes.Status409Conflict,
                Errors = ["This mapping has upload history and cannot be deleted. Deactivate it instead."]
            };
        }

        _dbTransaction.Commit();
        return new ServiceResponse { Message = "Distributor template mapping deleted successfully." };
    }

    internal static List<string> Validate(SaveDistributorTemplateMappingDto dto,
        IReadOnlyList<DistributorTemplateMapping> existingMappings)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.Name)) errors.Add("Mapping name is required.");
        if (string.IsNullOrWhiteSpace(dto.WorksheetName)) errors.Add("Worksheet name is required.");
        if (dto.HeaderRowNumber <= 0) errors.Add("Header row must be greater than zero.");
        if (dto.FirstDataRowNumber <= dto.HeaderRowNumber) errors.Add("First data row must be after the header row.");
        if (string.IsNullOrWhiteSpace(dto.HeaderSignature)) errors.Add("Header signature is required.");

        if (dto.Columns.Any(column => column.SourceColumnNumber <= 0))
            errors.Add("Every source column number must be greater than zero.");
        if (dto.Columns.Any(column => string.IsNullOrWhiteSpace(column.SourceHeader)))
            errors.Add("Every mapped source column must have a header.");
        if (dto.Columns.Any(column => !Enum.IsDefined(column.CanonicalField)))
            errors.Add("Every mapped column must use a valid canonical field.");
        if (dto.Columns.GroupBy(column => column.CanonicalField).Any(group => group.Count() > 1))
            errors.Add("A canonical field can only be mapped once.");

        var mapped = dto.Columns.Select(column => column.CanonicalField).ToHashSet();
        errors.AddRange(SelloutCanonicalFieldCatalog.RequiredFields
            .Where(field => !mapped.Contains(field))
            .OrderBy(field => field)
            .Select(field => $"Required canonical field {field} must be mapped."));

        var duplicateName = existingMappings.Any(mapping => mapping.Id != dto.Id &&
            string.Equals(mapping.Name.Trim(), dto.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (duplicateName) errors.Add("A mapping with this name already exists for the company.");
        return errors;
    }

    private static void Apply(SaveDistributorTemplateMappingDto dto, DistributorTemplateMapping mapping)
    {
        mapping.Name = dto.Name.Trim();
        mapping.WorksheetName = dto.WorksheetName.Trim();
        mapping.HeaderRowNumber = dto.HeaderRowNumber;
        mapping.FirstDataRowNumber = dto.FirstDataRowNumber;
        mapping.HeaderSignature = dto.HeaderSignature;
        mapping.Columns = ToEntities(dto.Columns);
    }

    private static List<DistributorTemplateColumn> ToEntities(IEnumerable<DistributorTemplateColumnDto> columns) =>
        columns.Select(column => new DistributorTemplateColumn
        {
            SourceColumnNumber = column.SourceColumnNumber,
            SourceHeader = column.SourceHeader.Trim(),
            CanonicalField = column.CanonicalField
        }).ToList();

    private static DistributorTemplateMappingDto ToDto(DistributorTemplateMapping mapping) => new()
    {
        Id = mapping.Id,
        CompanyId = mapping.CompanyId,
        Name = mapping.Name,
        WorksheetName = mapping.WorksheetName,
        HeaderRowNumber = mapping.HeaderRowNumber,
        FirstDataRowNumber = mapping.FirstDataRowNumber,
        HeaderSignature = mapping.HeaderSignature,
        IsActive = mapping.IsActive,
        CreatedDate = mapping.CreatedDate,
        CreatedBy = mapping.CreatedBy,
        ModifiedDate = mapping.ModifiedDate,
        ModifiedBy = mapping.ModifiedBy,
        Columns = mapping.Columns.OrderBy(column => column.SourceColumnNumber).Select(column =>
            new DistributorTemplateColumnDto
            {
                SourceColumnNumber = column.SourceColumnNumber,
                SourceHeader = column.SourceHeader,
                CanonicalField = column.CanonicalField
            }).ToList()
    };
}
