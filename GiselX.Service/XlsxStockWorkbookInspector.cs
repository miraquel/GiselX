using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Service;

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
    /// </summary>
    public async Task<StockMappingDetectionResultDto> DetectAsync(byte[] workbook,
        IReadOnlyCollection<StockTemplateMappingDto> mappings,
        CancellationToken cancellationToken = default)
    {
        var matches = new List<StockSheetMatchDto>();
        var rejections = new List<string>();
        var worksheetNames = new List<string>();
        foreach (var mapping in mappings.Where(mapping => mapping.IsActive))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preview = await InspectAsync(workbook, mapping.WorksheetName, mapping.HeaderRowNumber,
                mapping.FirstDataRowNumber, cancellationToken);
            if (preview.WorksheetNames.Count > worksheetNames.Count) worksheetNames = preview.WorksheetNames.ToList();
            if (preview.Errors.Count == 0 && string.Equals(preview.HeaderSignature, mapping.HeaderSignature,
                    StringComparison.Ordinal))
            {
                matches.Add(new StockSheetMatchDto
                {
                    Mapping = mapping,
                    WorksheetName = preview.WorksheetName ?? mapping.WorksheetName
                });
                continue;
            }

            rejections.Add($"{mapping.Name}: {XlsxWorkbookInspector.DescribeRejection(preview, mapping.HeaderSignature)}");
        }

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
}
