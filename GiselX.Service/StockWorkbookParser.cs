using System.Text.Json;
using GiselX.Domain;
using GiselX.Service.Dto;

namespace GiselX.Service;

public sealed class StockWorkbookParser
{
    private static readonly IReadOnlySet<StockCanonicalField> IdentityFields =
        new HashSet<StockCanonicalField>
        {
            StockCanonicalField.ProductId,
            StockCanonicalField.ProductName
        };

    /// <summary>
    /// Parses one worksheet with one mapping. <paramref name="worksheetName"/> overrides the mapping's own
    /// worksheet name with the name as resolved in the workbook, so a workbook contributing several mapped
    /// worksheets reads each one exactly where detection found it.
    /// </summary>
    public StockParseResult Parse(byte[] workbook, StockTemplateMappingDto mapping, string? worksheetName = null)
    {
        var rows = XlsxWorkbookInspector.ReadWorksheet(workbook, worksheetName ?? mapping.WorksheetName);
        var staged = new List<StockStagingRowDto>();
        var allErrors = new List<StockStagingErrorDto>();
        var sourceRows = rows
            .Where(row => row.Key >= mapping.FirstDataRowNumber)
            .Where(row => mapping.Columns.Any(column =>
                !string.IsNullOrWhiteSpace(row.Value.GetValueOrDefault(column.SourceColumnNumber))))
            .OrderBy(row => row.Key)
            .ToList();
        var ignoredSummaryRows = FindIgnorableRows(sourceRows, mapping);

        foreach (var source in sourceRows)
        {
            if (ignoredSummaryRows.Contains(source.Key))
            {
                continue;
            }

            var row = new StockStagingRowDto { SourceRowNumber = source.Key };
            var errors = new List<StockStagingErrorDto>();
            foreach (var column in mapping.Columns)
            {
                var raw = source.Value.GetValueOrDefault(column.SourceColumnNumber);
                if (column.CanonicalField is StockCanonicalField.Area or StockCanonicalField.Condition)
                {
                    // Kept exactly as typed: the converter would trim it, but the area and the Good/Bad
                    // lists are matched later by one shared normaliser, and the audit row should show what
                    // the distributor actually wrote.
                    Assign(row, column.CanonicalField, string.IsNullOrWhiteSpace(raw) ? null : raw);
                    continue;
                }

                if (!StockCellValueConverter.TryConvert(raw, column.CanonicalField, out var value))
                {
                    errors.Add(Error(source.Key, column.CanonicalField, raw,
                        UploadMessages.NotAValid(StockCanonicalFieldCatalog.GetValueType(column.CanonicalField))));
                    continue;
                }

                // A distributor leaves a quantity cell blank when the product carries no stock. Read that as
                // zero so the product still reaches the snapshot - but only for a column the template maps,
                // so an unmapped carton column stays "not given" rather than becoming a claimed zero.
                if (value is null && StockCanonicalFieldCatalog.ZeroWhenBlankFields.Contains(column.CanonicalField))
                    value = 0m;

                Assign(row, column.CanonicalField, value);
            }

            Validate(row, errors);
            row.IsValid = errors.Count == 0;
            row.ValidationErrorsJson = JsonSerializer.Serialize(errors);
            staged.Add(row);
            allErrors.AddRange(errors);
        }

        return new StockParseResult(
            staged,
            allErrors,
            staged.Count == 0 ? UploadMessages.NoDataRows : null,
            ignoredSummaryRows.OrderBy(row => row).ToList());
    }

    /// <summary>
    /// Finds the rows that are not stock lines at all. A row carrying no product identity can never be
    /// published, and it comes in two shapes: a total line, which pairs the missing identity with real
    /// figures and only ever sits under the data, and form noise such as a signature block
    /// ("Mengetahui" over a blank name), which carries no figure at all and can sit anywhere - including
    /// between the last product and the totals, where a purely backwards scan would stop short of it.
    /// A worksheet made up entirely of such rows is left alone: that points at the mapping rather than at
    /// the sheet, and reporting every row tells the user more than silently publishing nothing.
    /// </summary>
    private static IReadOnlySet<int> FindIgnorableRows(
        IReadOnlyList<KeyValuePair<int, IReadOnlyDictionary<int, string>>> sourceRows,
        StockTemplateMappingDto mapping)
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

    private static bool IsTotalRow(
        IReadOnlyDictionary<int, string> source,
        StockTemplateMappingDto mapping) =>
        HasNoProductIdentity(source, mapping) && HasNumericValue(source, mapping);

    private static bool IsFormNoiseRow(
        IReadOnlyDictionary<int, string> source,
        StockTemplateMappingDto mapping) =>
        HasNoProductIdentity(source, mapping) && !HasNumericValue(source, mapping);

    private static bool HasNoProductIdentity(
        IReadOnlyDictionary<int, string> source,
        StockTemplateMappingDto mapping) => mapping.Columns
        .Where(column => IdentityFields.Contains(column.CanonicalField))
        .All(column => string.IsNullOrWhiteSpace(source.GetValueOrDefault(column.SourceColumnNumber)));

    /// <summary>
    /// Asks whether a numeric column holds a figure, not merely whether it holds something. "Mengetahui"
    /// typed across a quantity column is a signature caption, not a total.
    /// </summary>
    private static bool HasNumericValue(
        IReadOnlyDictionary<int, string> source,
        StockTemplateMappingDto mapping) => mapping.Columns
        .Where(column => StockCanonicalFieldCatalog.GetValueType(column.CanonicalField) is var type &&
                         (type == typeof(int) || type == typeof(decimal)))
        .Any(column => StockCellValueConverter.TryConvert(
                           source.GetValueOrDefault(column.SourceColumnNumber),
                           column.CanonicalField,
                           out var value) && value is not null);

    private static void Validate(StockStagingRowDto row, List<StockStagingErrorDto> errors)
    {
        foreach (var field in StockCanonicalFieldCatalog.RequiredFields)
        {
            if (GetValue(row, field) is not (null or ""))
            {
                continue;
            }

            // A distributor leaves a quantity cell blank when the product carries no stock. Read that as
            // zero so the product still reaches the snapshot instead of the whole row being rejected.
            if (StockCanonicalFieldCatalog.GetValueType(field) == typeof(decimal))
            {
                Assign(row, field, 0m);
                continue;
            }

            errors.Add(Error(row.SourceRowNumber, field, null, UploadMessages.ValueRequired));
        }

        if (row.SaldoAkhir < 0)
        {
            errors.Add(Error(row.SourceRowNumber, StockCanonicalField.SaldoAkhir,
                row.SaldoAkhir.Value.ToString(), UploadMessages.MustNotBeNegative));
        }

        if (row.QtyCarton < 0)
        {
            errors.Add(Error(row.SourceRowNumber, StockCanonicalField.QtyCarton,
                row.QtyCarton.Value.ToString(), UploadMessages.MustNotBeNegative));
        }

        if (row.QtyPcs < 0)
        {
            errors.Add(Error(row.SourceRowNumber, StockCanonicalField.QtyPcs,
                row.QtyPcs.Value.ToString(), UploadMessages.MustNotBeNegative));
        }

        if (row.PriceUnit < 0)
        {
            errors.Add(Error(row.SourceRowNumber, StockCanonicalField.PriceUnit,
                row.PriceUnit.Value.ToString(), UploadMessages.MustNotBeNegative));
        }

        if (row.SmallestUnitQty < 0)
        {
            errors.Add(Error(row.SourceRowNumber, StockCanonicalField.SmallestUnitQty,
                row.SmallestUnitQty.Value.ToString(), UploadMessages.MustNotBeNegative));
        }

        foreach (var field in Enum.GetValues<StockCanonicalField>())
        {
            var limit = StockCanonicalFieldCatalog.GetMaxLength(field);
            if (limit.HasValue && GetValue(row, field) is string text && text.Length > limit.Value)
            {
                errors.Add(Error(row.SourceRowNumber, field, text,
                    UploadMessages.TooLong(limit.Value)));
            }
        }
    }

    private static object? GetValue(StockStagingRowDto row, StockCanonicalField field) => field switch
    {
        StockCanonicalField.ProductId => row.ProductId,
        StockCanonicalField.ProductName => row.ProductName,
        StockCanonicalField.ProductPackaging => row.ProductPackaging,
        StockCanonicalField.ProductPcsInCtn => row.ProductPcsInCtn,
        StockCanonicalField.ProductNetto => row.ProductNetto,
        StockCanonicalField.ProductUnit => row.ProductUnit,
        StockCanonicalField.SmallestUnit => row.SmallestUnit,
        StockCanonicalField.SmallestUnitQty => row.SmallestUnitQty,
        StockCanonicalField.PriceUnit => row.PriceUnit,
        StockCanonicalField.SaldoAwal => row.SaldoAwal,
        StockCanonicalField.SaldoMasukPO => row.SaldoMasukPO,
        StockCanonicalField.SaldoAkhir => row.SaldoAkhir,
        StockCanonicalField.BatchNumber => row.BatchNumber,
        StockCanonicalField.ExpiredDate => row.ExpiredDate,
        StockCanonicalField.Area => row.SourceAreaText,
        StockCanonicalField.Condition => row.SourceConditionText,
        StockCanonicalField.QtyCarton => row.QtyCarton,
        StockCanonicalField.QtyPcs => row.QtyPcs,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private static void Assign(StockStagingRowDto row, StockCanonicalField field, object? value)
    {
        switch (field)
        {
            case StockCanonicalField.ProductId: row.ProductId = (string?)value; break;
            case StockCanonicalField.ProductName: row.ProductName = (string?)value; break;
            case StockCanonicalField.ProductPackaging: row.ProductPackaging = (string?)value; break;
            case StockCanonicalField.ProductPcsInCtn: row.ProductPcsInCtn = (decimal?)value; break;
            case StockCanonicalField.ProductNetto: row.ProductNetto = (decimal?)value; break;
            case StockCanonicalField.ProductUnit: row.ProductUnit = (string?)value; break;
            case StockCanonicalField.SmallestUnit: row.SmallestUnit = (string?)value; break;
            case StockCanonicalField.SmallestUnitQty: row.SmallestUnitQty = (decimal?)value; break;
            case StockCanonicalField.PriceUnit: row.PriceUnit = (decimal?)value; break;
            case StockCanonicalField.SaldoAwal: row.SaldoAwal = (decimal?)value; break;
            case StockCanonicalField.SaldoMasukPO: row.SaldoMasukPO = (decimal?)value; break;
            case StockCanonicalField.SaldoAkhir: row.SaldoAkhir = (decimal?)value; break;
            case StockCanonicalField.BatchNumber: row.BatchNumber = (string?)value; break;
            case StockCanonicalField.ExpiredDate: row.ExpiredDate = (DateTime?)value; break;
            case StockCanonicalField.Area: row.SourceAreaText = (string?)value; break;
            case StockCanonicalField.Condition: row.SourceConditionText = (string?)value; break;
            case StockCanonicalField.QtyCarton: row.QtyCarton = (decimal?)value; break;
            case StockCanonicalField.QtyPcs: row.QtyPcs = (decimal?)value; break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
    }

    private static StockStagingErrorDto Error(
        int row,
        StockCanonicalField field,
        string? value,
        string reason) =>
        new() { SourceRowNumber = row, Field = field, RejectedValue = value, Reason = reason };
}
