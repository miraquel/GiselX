using System.Text.Json;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Service;

public sealed class StockWorkbookAnalysisService(
    IStockTemplateMappingRepository mappingRepository,
    IStockWorkbookInspector inspector,
    StockWorkbookParser parser,
    IStockAreaRepository areaRepository,
    ICompanyRepository companyRepository,
    IItemMappingResolver itemMappingResolver) : IStockWorkbookAnalysisService
{
    public async Task<StockWorkbookAnalysisDto> AnalyzeAsync(byte[] content, int companyId,
        CancellationToken cancellationToken)
    {
        var mappings = (await mappingRepository.GetActiveByCompanyAsync(companyId, cancellationToken))
            .Select(ToDto).ToList();
        var detection = await inspector.DetectAsync(content, mappings, cancellationToken);
        if (!detection.IsMatch)
            return new StockWorkbookAnalysisDto
            {
                Errors = detection.Errors,
                IgnoredWorksheetNames = detection.IgnoredWorksheetNames
            };

        var itemMappingContext = await itemMappingResolver.LoadAsync(companyId, cancellationToken);
        var activeAreas = await areaRepository.ListAsync(companyId, true, cancellationToken);
        var companyName = (await companyRepository.GetCompanyByIdAsync(companyId, cancellationToken))?.Name
            ?? "this distributor";
        var matcher = new StockAreaMatcher(activeAreas, companyName);

        if (activeAreas.Count > 0)
        {
            var refusals = detection.Matches
                .Where(match => match.Mapping.Columns.All(column => column.CanonicalField != StockCanonicalField.Area))
                .Select(match => match.Mapping)
                .DistinctBy(mapping => mapping.Id)
                .Select(mapping =>
                    $"Template '{mapping.Name}' does not map an Area column, but {companyName} has Stock areas registered.")
                .ToList();
            if (refusals.Count > 0)
                return new StockWorkbookAnalysisDto
                {
                    Errors = refusals,
                    IgnoredWorksheetNames = detection.IgnoredWorksheetNames
                };
        }

        var rows = new List<StockStagingRowDto>();
        var errors = new List<StockStagingErrorDto>();
        var ignoredSummaryRows = new List<int>();
        var sheets = new List<StockSheetAnalysisDto>();

        foreach (var match in detection.Matches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var parsed = parser.Parse(content, match.Mapping, match.WorksheetName);
            foreach (var error in parsed.Errors) error.WorksheetName = match.WorksheetName;
            var mapsArea = match.Mapping.Columns.Any(column => column.CanonicalField == StockCanonicalField.Area);
            var mapsCondition = match.Mapping.Columns.Any(column => column.CanonicalField == StockCanonicalField.Condition);
            foreach (var row in parsed.Rows)
            {
                row.StockTemplateMappingId = match.Mapping.Id;
                row.SourceWorksheetName = match.WorksheetName;
                var rowErrors = Resolve(row, itemMappingContext, match.WorksheetName, errors);
                ResolveAreaAndCondition(row, match.Mapping, mapsArea ? matcher : null, mapsCondition,
                    match.WorksheetName, rowErrors, errors);
                if (rowErrors.Count > 0) row.ValidationErrorsJson = JsonSerializer.Serialize(rowErrors);
            }

            rows.AddRange(parsed.Rows);
            errors.AddRange(parsed.Errors);
            ignoredSummaryRows.AddRange(parsed.IgnoredSummaryRowNumbers);
            sheets.Add(new StockSheetAnalysisDto
            {
                WorksheetName = match.WorksheetName,
                MappingId = match.Mapping.Id,
                MappingName = match.Mapping.Name,
                TotalRows = parsed.Rows.Count,
                ValidRows = parsed.Rows.Count(row => row.IsValid),
                InvalidRows = parsed.Rows.Count(row => !row.IsValid),
                IgnoredSummaryRowNumbers = parsed.IgnoredSummaryRowNumbers
            });
        }

        return new StockWorkbookAnalysisDto
        {
            Mapping = detection.Mapping,
            Sheets = sheets,
            IgnoredWorksheetNames = detection.IgnoredWorksheetNames,
            Rows = rows,
            IgnoredSummaryRowNumbers = ignoredSummaryRows.Distinct().OrderBy(row => row).ToList(),
            RowErrors = errors,
            // Only a workbook whose every mapped worksheet is empty is unusable; one empty sheet beside a
            // populated one is reported through its zero-row entry in Sheets.
            Errors = rows.Count == 0 ? ["The workbook contains no data rows."] : []
        };
    }

    private static void ResolveAreaAndCondition(StockStagingRowDto row, StockTemplateMappingDto mapping,
        StockAreaMatcher? areaMatcher, bool mapsCondition, string worksheetName,
        List<StockStagingErrorDto> rowErrors, List<StockStagingErrorDto> errors)
    {
        void Fail(StockCanonicalField field, string? rejected, string reason)
        {
            var error = new StockStagingErrorDto
            {
                SourceRowNumber = row.SourceRowNumber,
                WorksheetName = worksheetName,
                Field = field,
                RejectedValue = rejected,
                Reason = reason
            };
            rowErrors.Add(error);
            errors.Add(error);
            row.IsValid = false;
        }

        if (areaMatcher is not null)
        {
            var area = areaMatcher.Resolve(row.SourceAreaText);
            if (area.Error is not null) Fail(StockCanonicalField.Area, row.SourceAreaText, area.Error);
            else row.StockAreaId = area.AreaId;
        }

        if (mapsCondition)
        {
            var (condition, error) = StockConditionResolver.Resolve(row.SourceConditionText,
                mapping.GoodConditionValues, mapping.BadConditionValues, mapping.Name);
            if (error is not null) Fail(StockCanonicalField.Condition, row.SourceConditionText, error);
            else row.Condition = condition ?? StockCondition.Good;
        }
    }

    private List<StockStagingErrorDto> Resolve(StockStagingRowDto row, ItemMappingContextDto itemMappingContext,
        string worksheetName, List<StockStagingErrorDto> errors)
    {
        var resolution = itemMappingResolver.Resolve(itemMappingContext, row.ProductId, row.ProductName);
        row.DistributorProductId = resolution.DistributorItemId;
        row.DistributorProductName = resolution.DistributorItemName;
        row.ProductId = resolution.GmkItemId;
        row.ProductName = resolution.GmkItemName;
        row.ItemMappingId = resolution.ItemMappingId;

        var rowErrors = JsonSerializer.Deserialize<List<StockStagingErrorDto>>(row.ValidationErrorsJson) ?? [];
        foreach (var existing in rowErrors) existing.WorksheetName = worksheetName;
        if (resolution.ErrorReason is not null)
        {
            var error = new StockStagingErrorDto
            {
                SourceRowNumber = row.SourceRowNumber,
                WorksheetName = worksheetName,
                Field = StockCanonicalField.ProductId,
                RejectedValue = resolution.DistributorItemId,
                Reason = resolution.ErrorReason
            };
            rowErrors.Add(error);
            errors.Add(error);
            row.IsValid = false;
        }

        return rowErrors;
    }

    private static StockTemplateMappingDto ToDto(StockTemplateMapping mapping) => new()
    {
        Id = mapping.Id, CompanyId = mapping.CompanyId, Name = mapping.Name,
        WorksheetName = mapping.WorksheetName, HeaderRowNumber = mapping.HeaderRowNumber,
        FirstDataRowNumber = mapping.FirstDataRowNumber, HeaderSignature = mapping.HeaderSignature,
        IsActive = mapping.IsActive,
        GoodConditionValues = mapping.GoodConditionValues,
        BadConditionValues = mapping.BadConditionValues,
        Columns = mapping.Columns.Select(column => new StockTemplateColumnDto
        {
            SourceColumnNumber = column.SourceColumnNumber,
            SourceHeader = column.SourceHeader,
            CanonicalField = column.CanonicalField
        }).ToList()
    };
}
