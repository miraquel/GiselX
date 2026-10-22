using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Service;

/// <summary>
/// Matches a Sellout workbook against the company's active distributor template mappings. The reading of
/// the workbook itself belongs to <see cref="XlsxWorkbookInspector"/> and is shared with the Stock
/// pipeline; only the matching rules below are specific to Sellout.
/// </summary>
public sealed class XlsxSelloutWorkbookInspector : ISelloutWorkbookInspector
{
    private readonly XlsxWorkbookInspector _inspector = new();

    public Task<WorkbookPreviewDto> InspectAsync(byte[] workbook, string? worksheetName,
        int headerRowNumber, int firstDataRowNumber, CancellationToken cancellationToken = default) =>
        _inspector.InspectAsync(workbook, worksheetName, headerRowNumber, firstDataRowNumber, cancellationToken);

    /// <summary>
    /// Detection runs two rules. A mapping whose stored signature equals the workbook's header row matches
    /// exactly, which is the only rule a workbook that uploads cleanly today ever needs. Only when no
    /// mapping matched exactly is the second rule consulted: resolve each mapped column by its own header,
    /// which rescues a workbook that gained an unmapped column or had its columns reordered. Keeping the
    /// rules in that order means the looser one can never displace an exact match.
    /// </summary>
    public async Task<SelloutMappingDetectionResultDto> DetectAsync(
        byte[] workbook,
        IReadOnlyCollection<DistributorTemplateMappingDto> mappings,
        CancellationToken cancellationToken = default)
    {
        var exactMatches = new List<DistributorTemplateMappingDto>();
        var headerMatches = new List<DistributorTemplateMappingDto>();
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

            if (preview.Errors.Count > 0)
            {
                rejections.Add($"{mapping.Name}: {XlsxWorkbookInspector.DescribeRejection(preview, mapping.HeaderSignature)}");
                continue;
            }

            if (string.Equals(preview.HeaderSignature, mapping.HeaderSignature, StringComparison.Ordinal))
            {
                exactMatches.Add(mapping);
                continue;
            }

            var resolved = XlsxWorkbookInspector.ResolveColumnsByHeader(preview,
                mapping.Columns.Select(column => (column.SourceColumnNumber, column.SourceHeader)),
                out var missingHeader);
            if (resolved is not null)
            {
                headerMatches.Add(WithResolvedColumns(mapping, resolved));
                continue;
            }

            rejections.Add($"{mapping.Name}: {XlsxWorkbookInspector.DescribeRejection(preview, mapping.HeaderSignature)}"
                + XlsxWorkbookInspector.DescribeMissingHeader(missingHeader));
        }

        var matches = exactMatches.Count > 0 ? exactMatches : headerMatches;
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
                ? "Belum ada template Sellout aktif untuk perusahaan ini. " + UploadMessages.ContactGmk
                : "Format file tidak cocok dengan template Sellout aktif mana pun. Pastikan memakai format file yang biasa; "
                  + "bila formatnya memang berubah, hubungi admin PT. GMK.");
            result.Errors.AddRange(rejections);
        }
        else
        {
            result.Errors.Add("Lebih dari satu template Sellout aktif cocok dengan file ini: " +
                string.Join(", ", result.CandidateMappingNames) + ". Admin PT. GMK perlu menonaktifkan salah satunya.");
        }

        return result;
    }

    /// <summary>
    /// Copies the mapping with each column pointed at the position its header was found at, so the parser
    /// reads the right cells without knowing which rule matched.
    /// </summary>
    private static DistributorTemplateMappingDto WithResolvedColumns(
        DistributorTemplateMappingDto mapping, IReadOnlyDictionary<int, int> resolved) => new()
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
        Columns = mapping.Columns.Select(column => new DistributorTemplateColumnDto
        {
            SourceColumnNumber = resolved[column.SourceColumnNumber],
            SourceHeader = column.SourceHeader,
            CanonicalField = column.CanonicalField
        }).ToList()
    };
}
