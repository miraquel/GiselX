using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;

namespace GiselX.Service;

public sealed class StockTemplateMappingService(
    IStockTemplateMappingRepository repository,
    IDbTransaction dbTransaction,
    IStockAreaRepository areaRepository) : IStockTemplateMappingService
{
    public async Task<ServiceResponse<IReadOnlyList<StockTemplateMappingDto>>> ListAsync(int companyId,
        CancellationToken cancellationToken)
    {
        var mappings = await repository.GetByCompanyAsync(companyId, cancellationToken);
        return new ServiceResponse<IReadOnlyList<StockTemplateMappingDto>>
        {
            Data = mappings.Select(ToDto).ToList(),
            Message = "Stock template mappings retrieved successfully."
        };
    }

    public async Task<ServiceResponse<StockTemplateMappingDto?>> GetAsync(int id, int companyId,
        CancellationToken cancellationToken)
    {
        var mapping = await repository.GetByIdAsync(id, companyId, cancellationToken);
        return new ServiceResponse<StockTemplateMappingDto?>
        {
            Data = mapping is null ? null : ToDto(mapping),
            StatusCode = mapping is null ? StatusCodes.Status404NotFound : StatusCodes.Status200OK,
            Errors = mapping is null ? ["The Stock template mapping was not found."] : []
        };
    }

    public async Task<ServiceResponse<int>> SaveAsync(SaveStockTemplateMappingDto dto, string username,
        CancellationToken cancellationToken)
    {
        var existingMappings = await repository.GetByCompanyAsync(dto.CompanyId, cancellationToken);
        var errors = Validate(dto, existingMappings);

        // Uploads refuse a template without Area once the company has any area (active or not), so a
        // template saved that way could never be used. Refuse it here, where the admin can fix it.
        if (dto.Columns.All(column => column.CanonicalField != StockCanonicalField.Area))
        {
            var areas = await areaRepository.ListAsync(dto.CompanyId, false, cancellationToken);
            if (AreaRequirementError(dto.Columns, areas) is { } areaError) errors.Add(areaError);
        }

        if (errors.Count > 0)
        {
            return new ServiceResponse<int>
            {
                Errors = errors,
                Message = "The Stock template mapping is invalid.",
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var now = DateTime.UtcNow;
        int id;
        if (dto.Id.HasValue)
        {
            var current = await repository.GetByIdAsync(dto.Id.Value, dto.CompanyId, cancellationToken);
            if (current is null)
            {
                return new ServiceResponse<int>
                {
                    Errors = ["The Stock template mapping was not found."],
                    StatusCode = StatusCodes.Status404NotFound
                };
            }

            Apply(dto, current);
            current.ModifiedBy = username;
            current.ModifiedDate = now;
            await repository.UpdateAsync(current, cancellationToken);
            id = current.Id;
        }
        else
        {
            var mapping = new StockTemplateMapping
            {
                CompanyId = dto.CompanyId,
                Name = dto.Name.Trim(),
                WorksheetName = dto.WorksheetName.Trim(),
                HeaderRowNumber = dto.HeaderRowNumber,
                FirstDataRowNumber = dto.FirstDataRowNumber,
                HeaderSignature = dto.HeaderSignature,
                IsActive = true,
                GoodConditionValues = ConditionValues(dto, dto.GoodConditionValues),
                BadConditionValues = ConditionValues(dto, dto.BadConditionValues),
                CreatedBy = username,
                CreatedDate = now,
                Columns = ToEntities(dto.Columns)
            };
            id = await repository.InsertAsync(mapping, cancellationToken);
        }

        dbTransaction.Commit();
        return new ServiceResponse<int> { Data = id, Message = "Stock template mapping saved successfully." };
    }

    public async Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool isActive, string username,
        CancellationToken cancellationToken)
    {
        if (await repository.GetByIdAsync(id, companyId, cancellationToken) is null)
        {
            return new ServiceResponse
            {
                Errors = ["The Stock template mapping was not found."],
                StatusCode = StatusCodes.Status404NotFound
            };
        }

        await repository.SetActiveAsync(id, companyId, isActive, username, DateTime.UtcNow, cancellationToken);
        dbTransaction.Commit();
        return new ServiceResponse { Message = isActive ? "Mapping activated." : "Mapping deactivated." };
    }

    public async Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken cancellationToken)
    {
        var result = await repository.DeleteAsync(id, companyId, cancellationToken);
        if (result == StockTemplateMappingDeleteResult.NotFound)
            return new ServiceResponse { StatusCode = StatusCodes.Status404NotFound, Errors = ["The Stock template mapping was not found."] };
        if (result == StockTemplateMappingDeleteResult.HasUploadHistory)
            return new ServiceResponse
            {
                StatusCode = StatusCodes.Status409Conflict,
                Errors = ["This mapping has upload history and cannot be deleted. Deactivate it instead."]
            };

        dbTransaction.Commit();
        return new ServiceResponse { Message = "Stock template mapping deleted successfully." };
    }

    /// <summary>The refusal for a template without an Area column on a company that has Stock areas, or null.</summary>
    internal static string? AreaRequirementError(IEnumerable<StockTemplateColumnDto> columns,
        IReadOnlyList<StockArea> areas)
    {
        if (areas.Count == 0 || columns.Any(column => column.CanonicalField == StockCanonicalField.Area)) return null;
        return "This company has Stock areas registered (" + string.Join(", ", areas.Select(area => area.Name)) +
            "), so the template must map an Area column; uploads through a template without one are refused.";
    }

    internal static List<string> Validate(SaveStockTemplateMappingDto dto,
        IReadOnlyList<StockTemplateMapping> existingMappings)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(dto.Name)) errors.Add("Mapping name is required.");
        if (string.IsNullOrWhiteSpace(dto.WorksheetName)) errors.Add("Worksheet name is required.");
        if (dto.HeaderRowNumber <= 0) errors.Add("Header row must be greater than zero.");
        if (dto.FirstDataRowNumber <= dto.HeaderRowNumber) errors.Add("First data row must be after the header row.");
        if (string.IsNullOrWhiteSpace(dto.HeaderSignature)) errors.Add("Header signature is required.");
        if (dto.Columns.Any(column => column.SourceColumnNumber <= 0)) errors.Add("Every source column number must be greater than zero.");
        if (dto.Columns.Any(column => string.IsNullOrWhiteSpace(column.SourceHeader))) errors.Add("Every mapped source column must have a header.");
        if (dto.Columns.Any(column => !Enum.IsDefined(column.CanonicalField))) errors.Add("Every mapped column must use a valid canonical field.");
        if (dto.Columns.GroupBy(column => column.CanonicalField).Any(group => group.Count() > 1)) errors.Add("A canonical field can only be mapped once.");

        var mapped = dto.Columns.Select(column => column.CanonicalField).ToHashSet();
        errors.AddRange(StockCanonicalFieldCatalog.RequiredFields
            .Where(field => !mapped.Contains(field)).OrderBy(field => field)
            .Select(field => $"Required canonical field {field} must be mapped."));
        // The quantity comes as one column or as two (cartons and pieces), so at least one must be mapped.
        if (!StockCanonicalFieldCatalog.QuantityFields.Any(mapped.Contains))
            errors.Add("Map at least one stock quantity column: QtyCarton or QtyPcs.");
        ValidateConditionValues(dto, errors);
        if (existingMappings.Any(mapping => mapping.Id != dto.Id &&
            string.Equals(mapping.Name.Trim(), dto.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
            errors.Add("A mapping with this name already exists for the company.");
        return errors;
    }

    private static void Apply(SaveStockTemplateMappingDto dto, StockTemplateMapping mapping)
    {
        mapping.Name = dto.Name.Trim();
        mapping.WorksheetName = dto.WorksheetName.Trim();
        mapping.HeaderRowNumber = dto.HeaderRowNumber;
        mapping.FirstDataRowNumber = dto.FirstDataRowNumber;
        mapping.HeaderSignature = dto.HeaderSignature;
        mapping.GoodConditionValues = ConditionValues(dto, dto.GoodConditionValues);
        mapping.BadConditionValues = ConditionValues(dto, dto.BadConditionValues);
        mapping.Columns = ToEntities(dto.Columns);
    }

    private const int ConditionValuesMaxLength = 500;

    private static bool MapsCondition(SaveStockTemplateMappingDto dto) =>
        dto.Columns.Any(column => column.CanonicalField == StockCanonicalField.Condition);

    /// <summary>The stored form of a value list: normalised, ';'-joined, and null when empty or Condition is unmapped.</summary>
    private static string? ConditionValues(SaveStockTemplateMappingDto dto, string? input)
    {
        if (!MapsCondition(dto)) return null;
        var values = StockTextNormalizer.SplitList(input);
        return values.Count == 0 ? null : string.Join(";", values);
    }

    private static void ValidateConditionValues(SaveStockTemplateMappingDto dto, List<string> errors)
    {
        if (!MapsCondition(dto)) return;
        var good = StockTextNormalizer.SplitList(dto.GoodConditionValues);
        var bad = StockTextNormalizer.SplitList(dto.BadConditionValues);
        if (string.Join(";", good).Length > ConditionValuesMaxLength)
            errors.Add("Good values must not exceed 500 characters in total.");
        if (string.Join(";", bad).Length > ConditionValuesMaxLength)
            errors.Add("Bad values must not exceed 500 characters in total.");
        if (good.Count == 0 || bad.Count == 0)
        {
            errors.Add("Map at least one Good value and one Bad value when Condition is mapped.");
            return;
        }

        var badNormalised = bad.Select(StockTextNormalizer.Normalize).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var both = good.Where(value => badNormalised.Contains(StockTextNormalizer.Normalize(value))).ToList();
        if (both.Count > 0)
            errors.Add($"These values are listed as both Good and Bad: {string.Join(", ", both)}.");
    }

    private static List<StockTemplateColumn> ToEntities(IEnumerable<StockTemplateColumnDto> columns) =>
        columns.Select(column => new StockTemplateColumn
        {
            SourceColumnNumber = column.SourceColumnNumber,
            SourceHeader = column.SourceHeader.Trim(),
            CanonicalField = column.CanonicalField
        }).ToList();

    private static StockTemplateMappingDto ToDto(StockTemplateMapping mapping) => new()
    {
        Id = mapping.Id,
        CompanyId = mapping.CompanyId,
        Name = mapping.Name,
        WorksheetName = mapping.WorksheetName,
        HeaderRowNumber = mapping.HeaderRowNumber,
        FirstDataRowNumber = mapping.FirstDataRowNumber,
        HeaderSignature = mapping.HeaderSignature,
        IsActive = mapping.IsActive,
        GoodConditionValues = mapping.GoodConditionValues,
        BadConditionValues = mapping.BadConditionValues,
        CreatedDate = mapping.CreatedDate,
        CreatedBy = mapping.CreatedBy,
        ModifiedDate = mapping.ModifiedDate,
        ModifiedBy = mapping.ModifiedBy,
        Columns = mapping.Columns.OrderBy(column => column.SourceColumnNumber).Select(column => new StockTemplateColumnDto
        {
            SourceColumnNumber = column.SourceColumnNumber,
            SourceHeader = column.SourceHeader,
            CanonicalField = column.CanonicalField
        }).ToList()
    };
}
