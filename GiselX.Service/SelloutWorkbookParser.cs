using System.Text.Json;
using GiselX.Domain;
using GiselX.Service.Dto;

namespace GiselX.Service;

public sealed class SelloutWorkbookParser
{
    private static readonly IReadOnlySet<SelloutCanonicalField> IdentityFields =
        new HashSet<SelloutCanonicalField>
        {
            SelloutCanonicalField.CustomerId,
            SelloutCanonicalField.CustomerName,
            SelloutCanonicalField.ProductId,
            SelloutCanonicalField.ProductName,
            SelloutCanonicalField.InvoiceNo,
            SelloutCanonicalField.InvoiceDate,
            SelloutCanonicalField.InvoiceUnit
        };

    public SelloutParseResult Parse(byte[] workbook, DistributorTemplateMappingDto mapping)
    {
        var worksheet = XlsxWorkbookInspector.ReadWorksheetData(workbook, mapping.WorksheetName);
        var rows = XlsxWorkbookInspector.ExpandVerticalMergedValues(worksheet, mapping.FirstDataRowNumber);
        var staged = new List<SelloutStaging>();
        var allErrors = new List<SelloutStagingErrorDto>();
        var sourceRows = rows
            .Where(row => row.Key >= mapping.FirstDataRowNumber)
            .Where(row => mapping.Columns.Any(column =>
                !string.IsNullOrWhiteSpace(row.Value.GetValueOrDefault(column.SourceColumnNumber))))
            .OrderBy(row => row.Key)
            .ToList();
        var structurallyMergedSummaryRows = sourceRows
            .Where(row => IsStructurallyMergedSummaryRow(row.Key, worksheet.MergedRanges, mapping))
            .Select(row => row.Key)
            .ToHashSet();
        var remainingRows = sourceRows
            .Where(row => !structurallyMergedSummaryRows.Contains(row.Key))
            .ToList();
        var ignoredSummaryRows = FindIgnorableRows(remainingRows, mapping).ToHashSet();
        ignoredSummaryRows.UnionWith(structurallyMergedSummaryRows);

        foreach (var source in sourceRows)
        {
            if (ignoredSummaryRows.Contains(source.Key))
            {
                continue;
            }

            var row = new SelloutStaging { SourceRowNumber = source.Key };
            var errors = new List<SelloutStagingErrorDto>();
            foreach (var column in mapping.Columns)
            {
                var raw = source.Value.GetValueOrDefault(column.SourceColumnNumber);
                if (!SelloutCellValueConverter.TryConvert(raw, column.CanonicalField, out var value))
                {
                    errors.Add(Error(source.Key, column.CanonicalField, raw, UploadMessages.NotAValid(SelloutCanonicalFieldCatalog.GetValueType(column.CanonicalField))));
                    continue;
                }
                Assign(row, column.CanonicalField, value);
            }

            Validate(row, errors);
            row.IsValid = errors.Count == 0;
            row.ValidationErrorsJson = JsonSerializer.Serialize(errors);
            staged.Add(row);
            allErrors.AddRange(errors);
        }

        var weekSummaries = staged
            .Where(row => row.IsValid && row.InvoiceDate.HasValue)
            .GroupBy(row => GetMonday(row.InvoiceDate!.Value))
            .OrderBy(group => group.Key)
            .Select(group => new SelloutWeekSummaryDto
            {
                ReportingWeekStart = group.Key,
                RowCount = group.Count(),
                CoveredFrom = group.Min(row => row.InvoiceDate!.Value.Date),
                CoveredTo = group.Max(row => row.InvoiceDate!.Value.Date)
            })
            .ToList();

        return new SelloutParseResult(
            staged,
            allErrors,
            staged.Count == 0 ? UploadMessages.NoDataRows : null,
            weekSummaries,
            ignoredSummaryRows.OrderBy(row => row).ToList());
    }

    /// <summary>
    /// Finds the rows that are not sales lines at all. A row carrying no customer, product or invoice
    /// identity can never be published, and it comes in two shapes: a total line, which pairs the missing
    /// identity with real figures and only ever sits under the data, and form noise such as a signature
    /// block, which carries no figure at all and can sit anywhere - including between the last invoice
    /// and the totals, where a purely backwards scan would stop short of it. A worksheet made up entirely
    /// of such rows is left alone: that points at the mapping rather than at the sheet.
    /// </summary>
    private static IReadOnlySet<int> FindIgnorableRows(
        IReadOnlyList<KeyValuePair<int, IReadOnlyDictionary<int, string>>> sourceRows,
        DistributorTemplateMappingDto mapping)
    {
        var ignored = sourceRows
            .Where(row => IsFormNoiseRow(row.Value, mapping))
            .Select(row => row.Key)
            .ToHashSet();

        var index = sourceRows.Count - 1;
        while (index >= 0 &&
               (ignored.Contains(sourceRows[index].Key) || IsTotalRow(sourceRows[index].Value, mapping)))
        {
            ignored.Add(sourceRows[index].Key);
            index--;
        }

        if (index < 0)
        {
            ignored.Clear();
        }

        return ignored;
    }

    private static bool IsStructurallyMergedSummaryRow(
        int rowNumber,
        IReadOnlyList<XlsxWorkbookInspector.MergedRange> mergedRanges,
        DistributorTemplateMappingDto mapping)
    {
        return mergedRanges
            .Where(range => range.FirstRow == rowNumber && range.LastRow == rowNumber &&
                            range.LastColumn > range.FirstColumn)
            .Any(range => mapping.Columns.Count(column =>
                column.SourceColumnNumber >= range.FirstColumn &&
                column.SourceColumnNumber <= range.LastColumn) >= 2);
    }

    private static bool IsTotalRow(
        IReadOnlyDictionary<int, string> source,
        DistributorTemplateMappingDto mapping) =>
        HasNoSalesIdentity(source, mapping) && HasNumericValue(source, mapping);

    private static bool IsFormNoiseRow(
        IReadOnlyDictionary<int, string> source,
        DistributorTemplateMappingDto mapping) =>
        HasNoSalesIdentity(source, mapping) && !HasNumericValue(source, mapping);

    private static bool HasNoSalesIdentity(
        IReadOnlyDictionary<int, string> source,
        DistributorTemplateMappingDto mapping) => mapping.Columns
        .Where(column => IdentityFields.Contains(column.CanonicalField))
        .All(column => string.IsNullOrWhiteSpace(source.GetValueOrDefault(column.SourceColumnNumber)));

    /// <summary>
    /// Asks whether a numeric column holds a figure, not merely whether it holds something. A caption
    /// typed across a quantity column is a signature line, not a total.
    /// </summary>
    private static bool HasNumericValue(
        IReadOnlyDictionary<int, string> source,
        DistributorTemplateMappingDto mapping) => mapping.Columns
        .Where(column => SelloutCanonicalFieldCatalog.GetValueType(column.CanonicalField) is var type &&
                         (type == typeof(int) || type == typeof(decimal)))
        .Any(column => SelloutCellValueConverter.TryConvert(
                           source.GetValueOrDefault(column.SourceColumnNumber),
                           column.CanonicalField,
                           out var value) && value is not null);

    internal static DateTime GetMonday(DateTime date)
    {
        var offset = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-offset);
    }

    private static void Validate(SelloutStaging row, List<SelloutStagingErrorDto> errors)
    {
        foreach (var field in SelloutCanonicalFieldCatalog.RequiredFields)
        {
            if (GetValue(row, field) is null or "")
            {
                errors.Add(Error(row.SourceRowNumber, field, null, UploadMessages.ValueRequired));
            }
        }
        if (row.InvoiceQty == 0)
        {
            errors.Add(Error(row.SourceRowNumber, SelloutCanonicalField.InvoiceQty, row.InvoiceQty?.ToString(), "Invoice Qty tidak boleh nol."));
        }
        foreach (var field in Enum.GetValues<SelloutCanonicalField>())
        {
            var limit = SelloutCanonicalFieldCatalog.GetMaxLength(field);
            if (limit.HasValue && GetValue(row, field) is string text && text.Length > limit.Value)
            {
                errors.Add(Error(row.SourceRowNumber, field, text, UploadMessages.TooLong(limit.Value)));
            }
        }
    }

    private static object? GetValue(SelloutStaging row, SelloutCanonicalField field) => field switch
    {
        SelloutCanonicalField.CustomerId => row.CustomerId, SelloutCanonicalField.CustomerName => row.CustomerName,
        SelloutCanonicalField.CustomerAlias => row.CustomerAlias, SelloutCanonicalField.CustomerAddress => row.CustomerAddress,
        SelloutCanonicalField.ProductId => row.ProductId, SelloutCanonicalField.ProductBrand => row.ProductBrand,
        SelloutCanonicalField.ProductName => row.ProductName, SelloutCanonicalField.ProductPackaging => row.ProductPackaging,
        SelloutCanonicalField.ProductPcsInCtn => row.ProductPcsInCtn, SelloutCanonicalField.BatchNumber => row.BatchNumber,
        SelloutCanonicalField.ExpiredDate => row.ExpiredDate, SelloutCanonicalField.InvoiceNo => row.InvoiceNo,
        SelloutCanonicalField.InvoiceDate => row.InvoiceDate, SelloutCanonicalField.InvoiceQty => row.InvoiceQty,
        SelloutCanonicalField.InvoiceUnit => row.InvoiceUnit, SelloutCanonicalField.UnitPrice => row.UnitPrice,
        SelloutCanonicalField.GrossValue => row.GrossValue, SelloutCanonicalField.DiscountValue => row.DiscountValue,
        SelloutCanonicalField.DiscountPct => row.DiscountPct, SelloutCanonicalField.NetValue => row.NetValue,
        SelloutCanonicalField.InKg => row.InKg, SelloutCanonicalField.GeisaPOId => row.GeisaPOId,
        SelloutCanonicalField.ShipDate => row.ShipDate, SelloutCanonicalField.SalesmanNameGMK => row.SalesmanNameGMK,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private static void Assign(SelloutStaging row, SelloutCanonicalField field, object? value)
    {
        switch (field)
        {
            case SelloutCanonicalField.CustomerId: row.CustomerId = (string?)value; break;
            case SelloutCanonicalField.CustomerName: row.CustomerName = (string?)value; break;
            case SelloutCanonicalField.CustomerAlias: row.CustomerAlias = (string?)value; break;
            case SelloutCanonicalField.CustomerAddress: row.CustomerAddress = (string?)value; break;
            case SelloutCanonicalField.ProductId: row.ProductId = (string?)value; break;
            case SelloutCanonicalField.ProductBrand: row.ProductBrand = (string?)value; break;
            case SelloutCanonicalField.ProductName: row.ProductName = (string?)value; break;
            case SelloutCanonicalField.ProductPackaging: row.ProductPackaging = (string?)value; break;
            case SelloutCanonicalField.ProductPcsInCtn: row.ProductPcsInCtn = (decimal?)value; break;
            case SelloutCanonicalField.BatchNumber: row.BatchNumber = (string?)value; break;
            case SelloutCanonicalField.ExpiredDate: row.ExpiredDate = (DateTime?)value; break;
            case SelloutCanonicalField.InvoiceNo: row.InvoiceNo = (string?)value; break;
            case SelloutCanonicalField.InvoiceDate: row.InvoiceDate = (DateTime?)value; break;
            case SelloutCanonicalField.InvoiceQty: row.InvoiceQty = (decimal?)value; break;
            case SelloutCanonicalField.InvoiceUnit: row.InvoiceUnit = (string?)value; break;
            case SelloutCanonicalField.UnitPrice: row.UnitPrice = (decimal?)value; break;
            case SelloutCanonicalField.GrossValue: row.GrossValue = (decimal?)value; break;
            case SelloutCanonicalField.DiscountValue: row.DiscountValue = (decimal?)value; break;
            case SelloutCanonicalField.DiscountPct: row.DiscountPct = (decimal?)value; break;
            case SelloutCanonicalField.NetValue: row.NetValue = (decimal?)value; break;
            case SelloutCanonicalField.InKg: row.InKg = (decimal?)value; break;
            case SelloutCanonicalField.GeisaPOId: row.GeisaPOId = (string?)value; break;
            case SelloutCanonicalField.ShipDate: row.ShipDate = (DateTime?)value; break;
            case SelloutCanonicalField.SalesmanNameGMK: row.SalesmanNameGMK = (string?)value; break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
    }

    private static SelloutStagingErrorDto Error(int row, SelloutCanonicalField field, string? value, string reason) =>
        new() { SourceRowNumber = row, Field = field, RejectedValue = value, Reason = reason };
}
