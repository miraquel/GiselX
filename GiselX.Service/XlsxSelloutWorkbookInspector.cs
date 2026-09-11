using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Service;

/// <summary>
/// Matches a Sellout workbook against the company's active distributor template mappings. The reading of
/// the workbook itself belongs to <see cref="XlsxWorkbookInspector"/> and is shared with the Stock
/// pipeline; only the matching rule below is specific to Sellout.
/// </summary>
public sealed class XlsxSelloutWorkbookInspector : ISelloutWorkbookInspector
{
    private readonly XlsxWorkbookInspector _inspector = new();

    public Task<WorkbookPreviewDto> InspectAsync(byte[] workbook, string? worksheetName,
        int headerRowNumber, int firstDataRowNumber, CancellationToken cancellationToken = default) =>
        _inspector.InspectAsync(workbook, worksheetName, headerRowNumber, firstDataRowNumber, cancellationToken);

    public async Task<SelloutMappingDetectionResultDto> DetectAsync(
        byte[] workbook,
        IReadOnlyCollection<DistributorTemplateMappingDto> mappings,
        CancellationToken cancellationToken = default)
    {
        var matches = new List<DistributorTemplateMappingDto>();
        var rejections = new List<string>();
        foreach (var mapping in mappings.Where(mapping => mapping.IsActive))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preview = await InspectAsync(
                workbook,
                mapping.WorksheetName,
                mapping.HeaderRowNumber,
                mapping.FirstDataRowNumber,
                cancellationToken);
            if (preview.Errors.Count == 0 && string.Equals(preview.HeaderSignature, mapping.HeaderSignature, StringComparison.Ordinal))
            {
                matches.Add(mapping);
                continue;
            }

            rejections.Add($"{mapping.Name}: {XlsxWorkbookInspector.DescribeRejection(preview, mapping.HeaderSignature)}");
        }

        if (matches.Count == 1)
        {
            return new SelloutMappingDetectionResultDto { Mapping = matches[0] };
        }

        var result = new SelloutMappingDetectionResultDto
        {
            CandidateMappingNames = matches.Select(mapping => mapping.Name).OrderBy(name => name, StringComparer.Ordinal).ToList()
        };
        if (matches.Count == 0)
        {
            result.Errors.Add(rejections.Count == 0
                ? "No active distributor mapping is configured for this company."
                : "No active distributor mapping matches this workbook.");
            result.Errors.AddRange(rejections);
        }
        else
        {
            result.Errors.Add("Multiple active distributor mappings exactly match this workbook: " +
                string.Join(", ", result.CandidateMappingNames) + ". Deactivate all but one.");
        }

        return result;
    }
}
