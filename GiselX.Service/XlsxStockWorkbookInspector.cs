using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Service;

/// <summary>
/// Matches a Stock workbook against the company's active Stock template mappings. The reading of the
/// workbook itself belongs to <see cref="XlsxWorkbookInspector"/> and is shared with the Sellout pipeline;
/// only the matching rules below are specific to Stock.
/// </summary>
public sealed class XlsxStockWorkbookInspector : IStockWorkbookInspector
{
    private readonly XlsxWorkbookInspector _inspector = new();

    public Task<WorkbookPreviewDto> InspectAsync(byte[] workbook, string? worksheetName,
        int headerRowNumber, int firstDataRowNumber, CancellationToken cancellationToken = default) =>
        _inspector.InspectAsync(workbook, worksheetName, headerRowNumber, firstDataRowNumber, cancellationToken);

    /// <summary>
    /// Matches every active mapping against the worksheet it points at. A workbook may hold several
    /// mapped worksheets and all of them are accepted, because one file is one stock snapshot. Only two
    /// mappings claiming the same worksheet is still ambiguous: the workbook cannot say which applies.
    ///
    /// A mapping whose stored signature equals the worksheet's header row matches exactly. A mapping that
    /// does not is retried by resolving each of its columns to the column carrying that header, which
    /// rescues a worksheet that gained an unmapped column or had its columns reordered. That rescue is
    /// discarded for any worksheet some mapping already matched exactly, so a worksheet that resolves
    /// cleanly today cannot become contested.
    /// </summary>
    public async Task<StockMappingDetectionResultDto> DetectAsync(byte[] workbook,
        IReadOnlyCollection<StockTemplateMappingDto> mappings,
        CancellationToken cancellationToken = default)
    {
        var exactMatches = new List<StockSheetMatchDto>();
        var headerMatches = new List<StockSheetMatchDto>();
        var rejections = new List<string>();
        var worksheetNames = new List<string>();
        foreach (var mapping in mappings.Where(mapping => mapping.IsActive))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preview = await InspectAsync(workbook, mapping.WorksheetName, mapping.HeaderRowNumber,
                mapping.FirstDataRowNumber, cancellationToken);
            if (preview.WorksheetNames.Count > worksheetNames.Count) worksheetNames = preview.WorksheetNames.ToList();

            if (preview.Errors.Count > 0)
            {
                rejections.Add($"{mapping.Name}: {XlsxWorkbookInspector.DescribeRejection(preview, mapping.HeaderSignature)}");
                continue;
            }

            var worksheetName = preview.WorksheetName ?? mapping.WorksheetName;
            if (string.Equals(preview.HeaderSignature, mapping.HeaderSignature, StringComparison.Ordinal))
            {
                exactMatches.Add(new StockSheetMatchDto { Mapping = mapping, WorksheetName = worksheetName });
                continue;
            }

            var resolved = XlsxWorkbookInspector.ResolveColumnsByHeader(preview,
                mapping.Columns.Select(column => (column.SourceColumnNumber, column.SourceHeader)),
                out var missingHeader);
            if (resolved is not null)
            {
                headerMatches.Add(new StockSheetMatchDto
                {
                    Mapping = WithResolvedColumns(mapping, resolved),
                    WorksheetName = worksheetName
                });
                continue;
            }

            rejections.Add($"{mapping.Name}: {XlsxWorkbookInspector.DescribeRejection(preview, mapping.HeaderSignature)}"
                + XlsxWorkbookInspector.DescribeMissingHeader(missingHeader));
        }

        var settledWorksheets = exactMatches.Select(match => match.WorksheetName).ToHashSet(StringComparer.Ordinal);
        var matches = exactMatches
            .Concat(headerMatches.Where(match => !settledWorksheets.Contains(match.WorksheetName)))
            .ToList();

        var result = new StockMappingDetectionResultDto
        {
            CandidateMappingNames = matches.Select(match => match.Mapping.Name)
                .OrderBy(name => name, StringComparer.Ordinal).ToList()
        };

        if (matches.Count == 0)
        {
            result.Errors.Add(rejections.Count == 0
                ? "No active Stock mapping is configured for this company."
                : "No active Stock mapping matches this workbook.");
            result.Errors.AddRange(rejections);
            return result;
        }

        var contested = matches
            .GroupBy(match => match.WorksheetName, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToList();
        if (contested.Count > 0)
        {
            result.Errors.AddRange(contested.Select(group =>
                $"Multiple active Stock mappings match worksheet '{group.Key}': " +
                string.Join(", ", group.Select(match => match.Mapping.Name).OrderBy(name => name, StringComparer.Ordinal)) +
                ". Deactivate all but one."));
            return result;
        }

        result.Matches = matches.OrderBy(match => match.WorksheetName, StringComparer.Ordinal).ToList();
        var matched = matches.Select(match => match.WorksheetName).ToHashSet(StringComparer.Ordinal);
        result.IgnoredWorksheetNames = worksheetNames.Where(name => !matched.Contains(name)).ToList();
        return result;
    }

    /// <summary>
    /// Copies the mapping with each column pointed at the position its header was found at, so the parser
    /// reads the right cells without knowing which rule matched.
    /// </summary>
    private static StockTemplateMappingDto WithResolvedColumns(
        StockTemplateMappingDto mapping, IReadOnlyDictionary<int, int> resolved) => new()
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
        Columns = mapping.Columns.Select(column => new StockTemplateColumnDto
        {
            SourceColumnNumber = resolved[column.SourceColumnNumber],
            SourceHeader = column.SourceHeader,
            CanonicalField = column.CanonicalField
        }).ToList()
    };
}
