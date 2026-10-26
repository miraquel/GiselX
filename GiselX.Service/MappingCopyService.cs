using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;

namespace GiselX.Service;

public sealed class MappingCopyService(
    ICompanyRepository companyRepository,
    IDistributorTemplateMappingRepository selloutRepository,
    IStockTemplateMappingRepository stockRepository,
    IItemMappingRepository itemRepository,
    IStockAreaRepository areaRepository,
    IDbTransaction dbTransaction) : IMappingCopyService
{
    private const string NameExistsReason = "A mapping with this name already exists for the company.";
    private const string FormatExistsReason = "This company already has an active template for this format.";

    public async Task<IReadOnlyList<CopySourceCompanyDto>> ListSourceCompaniesAsync(int targetCompanyId,
        CancellationToken cancellationToken)
    {
        var companies = await companyRepository.GetAllAsync(cancellationToken);
        return companies.Where(company => company.Id != targetCompanyId)
            .Select(company => new CopySourceCompanyDto { Id = company.Id, DisplayName = DisplayName(company) })
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ServiceResponse<MappingCopyPreviewDto>> PreviewAsync(int sourceCompanyId, int targetCompanyId,
        CancellationToken cancellationToken)
    {
        var (state, refusal) = await LoadAsync(sourceCompanyId, targetCompanyId, cancellationToken);
        if (state is null) return Refuse<MappingCopyPreviewDto>(refusal!);

        var (itemsToCopy, itemsSkipped) = SplitItems(state);
        return new ServiceResponse<MappingCopyPreviewDto>
        {
            Data = new MappingCopyPreviewDto
            {
                SourceCompanyId = sourceCompanyId,
                TargetCompanyId = targetCompanyId,
                SelloutTemplates = EvaluateSellout(state, state.SourceSellout).Select(result => result.Candidate).ToList(),
                StockTemplates = EvaluateStock(state, state.SourceStock).Select(result => result.Candidate).ToList(),
                ItemMappingsToCopy = itemsToCopy.Count,
                ItemMappingsSkipped = itemsSkipped,
                TargetItemMappingEnabled = state.Target.ItemMappingEnabled
            }
        };
    }

    public async Task<ServiceResponse<MappingCopyResultDto>> CopyAsync(MappingCopyRequestDto request, string username,
        CancellationToken cancellationToken)
    {
        var (state, refusal) = await LoadAsync(request.SourceCompanyId, request.TargetCompanyId, cancellationToken);
        if (state is null) return Refuse<MappingCopyResultDto>(refusal!);

        // The preview is advisory: every selected item is re-evaluated against the data as it is now, so whatever
        // changed since (another admin, a new Stock area) is reported here rather than written.
        var result = new MappingCopyResultDto();
        var now = DateTime.UtcNow;
        var selloutIds = request.SelloutMappingIds.ToHashSet();
        foreach (var (mapping, candidate) in EvaluateSellout(state,
                     state.SourceSellout.Where(mapping => selloutIds.Contains(mapping.Id))))
        {
            if (candidate.Outcome != TemplateCopyOutcome.WillCopy)
            {
                result.Skipped.Add(Describe("Sellout", candidate));
                continue;
            }

            await selloutRepository.InsertAsync(CopySellout(mapping, state.Target.Id, username, now), cancellationToken);
            result.SelloutCopied++;
        }

        var stockIds = request.StockMappingIds.ToHashSet();
        foreach (var (mapping, candidate) in EvaluateStock(state,
                     state.SourceStock.Where(mapping => stockIds.Contains(mapping.Id))))
        {
            if (candidate.Outcome != TemplateCopyOutcome.WillCopy)
            {
                result.Skipped.Add(Describe("Stock", candidate));
                continue;
            }

            await stockRepository.InsertAsync(CopyStock(mapping, state.Target.Id, username, now), cancellationToken);
            result.StockCopied++;
        }

        if (request.CopyItemMappings)
        {
            var (itemsToCopy, itemsSkipped) = SplitItems(state);
            foreach (var item in itemsToCopy)
                await itemRepository.InsertAsync(CopyItem(item, state.Target.Id, username, now), cancellationToken);
            result.ItemMappingsCopied = itemsToCopy.Count;
            result.ItemMappingsSkipped = itemsSkipped;
        }

        if (result.SelloutCopied + result.StockCopied + result.ItemMappingsCopied == 0)
            return new ServiceResponse<MappingCopyResultDto> { Data = result, Message = "Nothing was copied." };

        dbTransaction.Commit();
        return new ServiceResponse<MappingCopyResultDto> { Data = result, Message = "Mappings copied." };
    }

    private async Task<(CopyState? State, ServiceResponse? Refusal)> LoadAsync(int sourceCompanyId,
        int targetCompanyId, CancellationToken cancellationToken)
    {
        if (sourceCompanyId == targetCompanyId)
            return (null, new ServiceResponse
            {
                Errors = ["Choose a different company to copy from."],
                StatusCode = StatusCodes.Status400BadRequest
            });

        var source = await companyRepository.GetCompanyByIdAsync(sourceCompanyId, cancellationToken);
        var target = await companyRepository.GetCompanyByIdAsync(targetCompanyId, cancellationToken);
        if (source is null || target is null)
            return (null, new ServiceResponse
            {
                Errors = ["The company was not found."],
                StatusCode = StatusCodes.Status404NotFound
            });

        return (new CopyState(
            target,
            OrderByName(await selloutRepository.GetByCompanyAsync(sourceCompanyId, cancellationToken), m => m.Name),
            await selloutRepository.GetByCompanyAsync(targetCompanyId, cancellationToken),
            OrderByName(await stockRepository.GetByCompanyAsync(sourceCompanyId, cancellationToken), m => m.Name),
            await stockRepository.GetByCompanyAsync(targetCompanyId, cancellationToken),
            await areaRepository.ListAsync(targetCompanyId, false, cancellationToken),
            await itemRepository.GetByCompanyAsync(sourceCompanyId, cancellationToken),
            await itemRepository.GetByCompanyAsync(targetCompanyId, cancellationToken)), null);
    }

    private static List<(DistributorTemplateMapping Mapping, TemplateCopyCandidateDto Candidate)> EvaluateSellout(
        CopyState state, IEnumerable<DistributorTemplateMapping> candidates)
    {
        var evaluator = new TemplateEvaluator(
            state.TargetSellout.Select(mapping => mapping.Name),
            state.TargetSellout.Where(mapping => mapping.IsActive).Select(SelloutFormat));
        return candidates.Select(mapping =>
        {
            // The target's own templates are passed as an empty list: a name clash is reported by the evaluator as
            // a skip, not as a validation failure.
            var errors = DistributorTemplateMappingService.Validate(ToSaveDto(mapping, state.Target.Id), []);
            return (mapping, evaluator.Evaluate(Candidate(mapping.Id, mapping.Name, mapping.WorksheetName,
                mapping.HeaderRowNumber, mapping.FirstDataRowNumber, mapping.Columns.Count, mapping.IsActive),
                SelloutFormat(mapping), errors));
        }).ToList();
    }

    private static List<(StockTemplateMapping Mapping, TemplateCopyCandidateDto Candidate)> EvaluateStock(
        CopyState state, IEnumerable<StockTemplateMapping> candidates)
    {
        var evaluator = new TemplateEvaluator(
            state.TargetStock.Select(mapping => mapping.Name),
            state.TargetStock.Where(mapping => mapping.IsActive).Select(StockFormat));
        return candidates.Select(mapping =>
        {
            var dto = ToSaveDto(mapping, state.Target.Id);
            var errors = StockTemplateMappingService.Validate(dto, []);
            if (StockTemplateMappingService.AreaRequirementError(dto.Columns, state.TargetAreas) is { } areaError)
                errors.Add(areaError);
            return (mapping, evaluator.Evaluate(Candidate(mapping.Id, mapping.Name, mapping.WorksheetName,
                mapping.HeaderRowNumber, mapping.FirstDataRowNumber, mapping.Columns.Count, mapping.IsActive),
                StockFormat(mapping), errors));
        }).ToList();
    }

    // Uploads choose a template by header signature, not by name, and refuse a file two active templates match.
    // Sellout compares the signature alone; Stock only refuses two active matches on the same worksheet.
    private static string SelloutFormat(DistributorTemplateMapping mapping) => mapping.HeaderSignature;

    private static string StockFormat(StockTemplateMapping mapping) =>
        mapping.WorksheetName.Trim().ToUpperInvariant() + "\0" + mapping.HeaderSignature;

    private static (List<ItemMapping> ToCopy, int Skipped) SplitItems(CopyState state)
    {
        var known = state.TargetItems.Select(item => item.DistributorItemId.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toCopy = new List<ItemMapping>();
        var skipped = 0;
        foreach (var item in state.SourceItems.OrderBy(item => item.DistributorItemId.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            if (known.Add(item.DistributorItemId.Trim())) toCopy.Add(item);
            else skipped++;
        }

        return (toCopy, skipped);
    }

    private static TemplateCopyCandidateDto Candidate(int id, string name, string worksheetName, int headerRow,
        int firstDataRow, int columnCount, bool isActive) => new()
    {
        SourceId = id,
        Name = name,
        WorksheetName = worksheetName,
        HeaderRowNumber = headerRow,
        FirstDataRowNumber = firstDataRow,
        ColumnCount = columnCount,
        IsActive = isActive
    };

    private static string Describe(string kind, TemplateCopyCandidateDto candidate) =>
        $"{kind} '{candidate.Name}': {string.Join(" ", candidate.Reasons)}";

    private static SaveDistributorTemplateMappingDto ToSaveDto(DistributorTemplateMapping mapping, int companyId) => new()
    {
        CompanyId = companyId,
        Name = mapping.Name,
        WorksheetName = mapping.WorksheetName,
        HeaderRowNumber = mapping.HeaderRowNumber,
        FirstDataRowNumber = mapping.FirstDataRowNumber,
        HeaderSignature = mapping.HeaderSignature,
        Columns = mapping.Columns.Select(column => new DistributorTemplateColumnDto
        {
            SourceColumnNumber = column.SourceColumnNumber,
            SourceHeader = column.SourceHeader,
            CanonicalField = column.CanonicalField
        }).ToList()
    };

    private static SaveStockTemplateMappingDto ToSaveDto(StockTemplateMapping mapping, int companyId) => new()
    {
        CompanyId = companyId,
        Name = mapping.Name,
        WorksheetName = mapping.WorksheetName,
        HeaderRowNumber = mapping.HeaderRowNumber,
        FirstDataRowNumber = mapping.FirstDataRowNumber,
        HeaderSignature = mapping.HeaderSignature,
        GoodConditionValues = mapping.GoodConditionValues,
        BadConditionValues = mapping.BadConditionValues,
        Columns = mapping.Columns.Select(column => new StockTemplateColumnDto
        {
            SourceColumnNumber = column.SourceColumnNumber,
            SourceHeader = column.SourceHeader,
            CanonicalField = column.CanonicalField
        }).ToList()
    };

    private static DistributorTemplateMapping CopySellout(DistributorTemplateMapping source, int companyId,
        string username, DateTime now) => new()
    {
        CompanyId = companyId,
        Name = source.Name.Trim(),
        WorksheetName = source.WorksheetName,
        HeaderRowNumber = source.HeaderRowNumber,
        FirstDataRowNumber = source.FirstDataRowNumber,
        HeaderSignature = source.HeaderSignature,
        IsActive = source.IsActive,
        CreatedBy = username,
        CreatedDate = now,
        Columns = source.Columns.Select(column => new DistributorTemplateColumn
        {
            SourceColumnNumber = column.SourceColumnNumber,
            SourceHeader = column.SourceHeader,
            CanonicalField = column.CanonicalField
        }).ToList()
    };

    private static StockTemplateMapping CopyStock(StockTemplateMapping source, int companyId, string username,
        DateTime now) => new()
    {
        CompanyId = companyId,
        Name = source.Name.Trim(),
        WorksheetName = source.WorksheetName,
        HeaderRowNumber = source.HeaderRowNumber,
        FirstDataRowNumber = source.FirstDataRowNumber,
        HeaderSignature = source.HeaderSignature,
        IsActive = source.IsActive,
        GoodConditionValues = source.GoodConditionValues,
        BadConditionValues = source.BadConditionValues,
        CreatedBy = username,
        CreatedDate = now,
        Columns = source.Columns.Select(column => new StockTemplateColumn
        {
            SourceColumnNumber = column.SourceColumnNumber,
            SourceHeader = column.SourceHeader,
            CanonicalField = column.CanonicalField
        }).ToList()
    };

    private static ItemMapping CopyItem(ItemMapping source, int companyId, string username, DateTime now) => new()
    {
        CompanyId = companyId,
        GmkItemId = source.GmkItemId.Trim(),
        GmkItemName = source.GmkItemName.Trim(),
        DistributorItemId = source.DistributorItemId.Trim(),
        DistributorItemName = source.DistributorItemName.Trim(),
        IsActive = source.IsActive,
        CreatedBy = username,
        CreatedDate = now
    };

    private static IReadOnlyList<T> OrderByName<T>(IEnumerable<T> values, Func<T, string> name) =>
        values.OrderBy(value => name(value).Trim(), StringComparer.OrdinalIgnoreCase).ToList();

    private static string DisplayName(Company company) =>
        string.IsNullOrWhiteSpace(company.Location) ? company.Name : $"{company.Name} – {company.Location.Trim()}";

    private static ServiceResponse<T> Refuse<T>(ServiceResponse refusal) => new()
    {
        Errors = refusal.Errors,
        StatusCode = refusal.StatusCode
    };

    /// <summary>
    /// Decides each candidate in order, remembering the names and active formats of the ones it accepts so two
    /// copies cannot clash with each other any more than with what the target already has.
    /// </summary>
    private sealed class TemplateEvaluator(IEnumerable<string> existingNames, IEnumerable<string> activeFormats)
    {
        private readonly HashSet<string> _names = existingNames.Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _formats = activeFormats.ToHashSet(StringComparer.Ordinal);

        public TemplateCopyCandidateDto Evaluate(TemplateCopyCandidateDto candidate, string format,
            IReadOnlyList<string> validationErrors)
        {
            if (_names.Contains(candidate.Name.Trim()))
            {
                candidate.Outcome = TemplateCopyOutcome.SkippedNameExists;
                candidate.Reasons = [NameExistsReason];
            }
            else if (candidate.IsActive && _formats.Contains(format))
            {
                candidate.Outcome = TemplateCopyOutcome.SkippedFormatExists;
                candidate.Reasons = [FormatExistsReason];
            }
            else if (validationErrors.Count > 0)
            {
                candidate.Outcome = TemplateCopyOutcome.Invalid;
                candidate.Reasons = validationErrors.ToList();
            }
            else
            {
                candidate.Outcome = TemplateCopyOutcome.WillCopy;
                _names.Add(candidate.Name.Trim());
                if (candidate.IsActive) _formats.Add(format);
            }

            return candidate;
        }
    }

    private sealed record CopyState(
        Company Target,
        IReadOnlyList<DistributorTemplateMapping> SourceSellout,
        IReadOnlyList<DistributorTemplateMapping> TargetSellout,
        IReadOnlyList<StockTemplateMapping> SourceStock,
        IReadOnlyList<StockTemplateMapping> TargetStock,
        IReadOnlyList<StockArea> TargetAreas,
        IReadOnlyList<ItemMapping> SourceItems,
        IReadOnlyList<ItemMapping> TargetItems);
}
