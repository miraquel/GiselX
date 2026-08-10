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

    public StockParseResult Parse(byte[] workbook, StockTemplateMappingDto mapping)
    {
        var rows = XlsxSelloutWorkbookInspector.ReadWorksheet(workbook, mapping.WorksheetName);
        var staged = new List<StockStagingRowDto>();
        var allErrors = new List<StockStagingErrorDto>();
        var sourceRows = rows
            .Where(row => row.Key >= mapping.FirstDataRowNumber)
            .Where(row => mapping.Columns.Any(column =>
                !string.IsNullOrWhiteSpace(row.Value.GetValueOrDefault(column.SourceColumnNumber))))
            .OrderBy(row => row.Key)
            .ToList();
        var ignoredSummaryRows = FindTrailingSummaryRows(sourceRows, mapping);

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
                if (!StockCellValueConverter.TryConvert(raw, column.CanonicalField, out var value))
                {
                    errors.Add(Error(source.Key, column.CanonicalField, raw,
                        $"Value is not a valid {StockCanonicalFieldCatalog.GetValueType(column.CanonicalField).Name}."));
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

        return new StockParseResult(
            staged,
            allErrors,
            staged.Count == 0 ? "The workbook contains no data rows." : null,
            ignoredSummaryRows.OrderBy(row => row).ToList());
    }

    private static IReadOnlySet<int> FindTrailingSummaryRows(
        IReadOnlyList<KeyValuePair<int, IReadOnlyDictionary<int, string>>> sourceRows,
        StockTemplateMappingDto mapping)
    {
        var ignored = new HashSet<int>();
        var index = sourceRows.Count - 1;
        while (index >= 0 && IsSummaryRow(sourceRows[index].Value, mapping))
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

    private static bool IsSummaryRow(
        IReadOnlyDictionary<int, string> source,
        StockTemplateMappingDto mapping)
    {
        var identityValuesAreBlank = mapping.Columns
            .Where(column => IdentityFields.Contains(column.CanonicalField))
            .All(column => string.IsNullOrWhiteSpace(source.GetValueOrDefault(column.SourceColumnNumber)));
        var hasNumericValue = mapping.Columns
            .Where(column => StockCanonicalFieldCatalog.GetValueType(column.CanonicalField) is var type &&
                             (type == typeof(int) || type == typeof(decimal)))
            .Any(column => !string.IsNullOrWhiteSpace(source.GetValueOrDefault(column.SourceColumnNumber)));

        return identityValuesAreBlank && hasNumericValue;
    }

    private static void Validate(StockStagingRowDto row, List<StockStagingErrorDto> errors)
    {
        foreach (var field in StockCanonicalFieldCatalog.RequiredFields)
        {
            if (GetValue(row, field) is null or "")
            {
                errors.Add(Error(row.SourceRowNumber, field, null, "A value is required."));
            }
        }

        if (row.SaldoAkhir < 0)
        {
            errors.Add(Error(row.SourceRowNumber, StockCanonicalField.SaldoAkhir,
                row.SaldoAkhir.Value.ToString(), "SaldoAkhir must not be negative."));
        }

        if (row.PriceUnit < 0)
        {
            errors.Add(Error(row.SourceRowNumber, StockCanonicalField.PriceUnit,
                row.PriceUnit.Value.ToString(), "PriceUnit must not be negative."));
        }

        foreach (var field in Enum.GetValues<StockCanonicalField>())
        {
            var limit = StockCanonicalFieldCatalog.GetMaxLength(field);
            if (limit.HasValue && GetValue(row, field) is string text && text.Length > limit.Value)
            {
                errors.Add(Error(row.SourceRowNumber, field, text,
                    $"Value exceeds the maximum length of {limit.Value}."));
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
        StockCanonicalField.PriceUnit => row.PriceUnit,
        StockCanonicalField.SaldoAwal => row.SaldoAwal,
        StockCanonicalField.SaldoMasukPO => row.SaldoMasukPO,
        StockCanonicalField.SaldoAkhir => row.SaldoAkhir,
        StockCanonicalField.BatchNumber => row.BatchNumber,
        StockCanonicalField.ExpiredDate => row.ExpiredDate,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };

    private static void Assign(StockStagingRowDto row, StockCanonicalField field, object? value)
    {
        switch (field)
        {
            case StockCanonicalField.ProductId: row.ProductId = (string?)value; break;
            case StockCanonicalField.ProductName: row.ProductName = (string?)value; break;
            case StockCanonicalField.ProductPackaging: row.ProductPackaging = (string?)value; break;
            case StockCanonicalField.ProductPcsInCtn: row.ProductPcsInCtn = (int?)value; break;
            case StockCanonicalField.ProductNetto: row.ProductNetto = (decimal?)value; break;
            case StockCanonicalField.ProductUnit: row.ProductUnit = (string?)value; break;
            case StockCanonicalField.PriceUnit: row.PriceUnit = (decimal?)value; break;
            case StockCanonicalField.SaldoAwal: row.SaldoAwal = (decimal?)value; break;
            case StockCanonicalField.SaldoMasukPO: row.SaldoMasukPO = (decimal?)value; break;
            case StockCanonicalField.SaldoAkhir: row.SaldoAkhir = (decimal?)value; break;
            case StockCanonicalField.BatchNumber: row.BatchNumber = (string?)value; break;
            case StockCanonicalField.ExpiredDate: row.ExpiredDate = (DateTime?)value; break;
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
