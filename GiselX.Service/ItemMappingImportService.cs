using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;

namespace GiselX.Service;

public sealed class ItemMappingImportService(
    ItemMappingWorkbookParser parser,
    IItemMappingRepository repository,
    IDbTransaction transaction) : IItemMappingImportService
{
    public async Task<ServiceResponse<ItemMappingImportPreviewDto>> PreflightAsync(byte[] workbook, int companyId,
        CancellationToken cancellationToken)
    {
        var analysis = await AnalyzeAsync(workbook, companyId, username: null, cancellationToken);
        return new ServiceResponse<ItemMappingImportPreviewDto>
        {
            Data = analysis.Preview,
            Errors = analysis.Preview.Errors.Select(error => Format(error)).ToList(),
            Message = analysis.Preview.CanImport ? "Item mapping workbook analysis completed." : "Item mapping workbook validation failed.",
            StatusCode = analysis.Preview.CanImport ? StatusCodes.Status200OK : StatusCodes.Status400BadRequest
        };
    }

    public async Task<ServiceResponse<ItemMappingImportResultDto>> ImportAsync(byte[] workbook, int companyId,
        bool confirmed, string username, CancellationToken cancellationToken)
    {
        if (!confirmed)
        {
            return new ServiceResponse<ItemMappingImportResultDto>
            {
                Errors = ["Review the workbook and confirm the item mapping import before continuing."],
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var analysis = await AnalyzeAsync(workbook, companyId, username, cancellationToken);
        var result = ToResult(analysis.Preview);
        if (!analysis.Preview.CanImport)
        {
            return new ServiceResponse<ItemMappingImportResultDto>
            {
                Data = result,
                Errors = analysis.Preview.Errors.Select(error => Format(error)).ToList(),
                Message = "Item mapping workbook validation failed.",
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        if (analysis.Values.Count > 0)
        {
            await repository.UpsertAsync(analysis.Values, cancellationToken);
        }
        transaction.Commit();
        return new ServiceResponse<ItemMappingImportResultDto>
        {
            Data = result,
            Message = "Item mappings imported successfully."
        };
    }

    private async Task<Analysis> AnalyzeAsync(byte[] workbook, int companyId, string? username,
        CancellationToken cancellationToken)
    {
        var parsed = parser.Parse(workbook);
        var preview = new ItemMappingImportPreviewDto
        {
            InvalidRows = parsed.InvalidRows,
            Errors = parsed.Errors
        };
        var existingMappings = await repository.GetByCompanyAsync(companyId, cancellationToken);
        var existingByDistributorItemId = existingMappings.ToDictionary(
            mapping => ItemMappingWorkbookParser.Normalize(mapping.DistributorItemId), StringComparer.OrdinalIgnoreCase);
        var values = new List<ItemMapping>();
        var now = DateTime.UtcNow;
        foreach (var parsedMapping in parsed.Rows)
        {
            if (!existingByDistributorItemId.TryGetValue(ItemMappingWorkbookParser.Normalize(parsedMapping.DistributorItemId),
                    out var existing))
            {
                preview.NewRows++;
                if (username is not null)
                {
                    values.Add(new ItemMapping
                    {
                        CompanyId = companyId, GmkItemId = parsedMapping.GmkItemId, GmkItemName = parsedMapping.GmkItemName,
                        DistributorItemId = parsedMapping.DistributorItemId, DistributorItemName = parsedMapping.DistributorItemName,
                        IsActive = parsedMapping.IsActive, CreatedBy = username, CreatedDate = now
                    });
                }
                continue;
            }

            if (IsUnchanged(parsedMapping, existing))
            {
                preview.UnchangedRows++;
                continue;
            }

            preview.UpdatedRows++;
            if (username is not null)
            {
                values.Add(new ItemMapping
                {
                    Id = existing.Id, CompanyId = existing.CompanyId, GmkItemId = parsedMapping.GmkItemId,
                    GmkItemName = parsedMapping.GmkItemName, DistributorItemId = parsedMapping.DistributorItemId,
                    DistributorItemName = parsedMapping.DistributorItemName, IsActive = parsedMapping.IsActive,
                    CreatedBy = existing.CreatedBy, CreatedDate = existing.CreatedDate, ModifiedBy = username, ModifiedDate = now
                });
            }
        }

        return new Analysis(preview, values);
    }

    private static bool IsUnchanged(ItemMapping incoming, ItemMapping existing) =>
        string.Equals(incoming.GmkItemId.Trim(), existing.GmkItemId.Trim(), StringComparison.Ordinal) &&
        string.Equals(incoming.GmkItemName.Trim(), existing.GmkItemName.Trim(), StringComparison.Ordinal) &&
        string.Equals(incoming.DistributorItemId.Trim(), existing.DistributorItemId.Trim(), StringComparison.OrdinalIgnoreCase) &&
        string.Equals(incoming.DistributorItemName.Trim(), existing.DistributorItemName.Trim(), StringComparison.Ordinal) &&
        incoming.IsActive == existing.IsActive;

    private static ItemMappingImportResultDto ToResult(ItemMappingImportPreviewDto preview) => new()
    {
        NewRows = preview.NewRows, UpdatedRows = preview.UpdatedRows, UnchangedRows = preview.UnchangedRows,
        InvalidRows = preview.InvalidRows, Errors = preview.Errors
    };

    private static string Format(ItemMappingImportErrorDto error) =>
        $"Row {error.SourceRowNumber}, {error.Field}: {error.Reason}";

    private sealed record Analysis(ItemMappingImportPreviewDto Preview, IReadOnlyList<ItemMapping> Values);
}
