using GiselX.Domain;

namespace GiselX.Service;

/// <summary>
/// Converts a Sellout cell to its canonical field's type. The conversion rules are shared with the Stock
/// pipeline in <see cref="WorkbookCellValueConverter"/>; only the field-to-type map is specific here.
/// </summary>
public static class SelloutCellValueConverter
{
    public static bool TryConvert(string? source, SelloutCanonicalField field, out object? value) =>
        WorkbookCellValueConverter.TryConvert(
            source, SelloutCanonicalFieldCatalog.GetValueType(field), out value);
}
