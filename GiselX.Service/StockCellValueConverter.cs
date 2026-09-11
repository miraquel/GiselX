using GiselX.Domain;

namespace GiselX.Service;

/// <summary>
/// Converts a Stock cell to its canonical field's type. The conversion rules are shared with the Sellout
/// pipeline in <see cref="WorkbookCellValueConverter"/>; only the field-to-type map is specific here.
/// </summary>
public static class StockCellValueConverter
{
    public static bool TryConvert(string? source, StockCanonicalField field, out object? value) =>
        WorkbookCellValueConverter.TryConvert(
            source, StockCanonicalFieldCatalog.GetValueType(field), out value);
}
