using GiselX.Domain;

namespace GiselX.Service;

public static class StockCanonicalFieldCatalog
{
    public static IReadOnlySet<StockCanonicalField> RequiredFields { get; } =
        new HashSet<StockCanonicalField>
        {
            StockCanonicalField.ProductId,
            StockCanonicalField.ProductName,
            StockCanonicalField.SaldoAkhir
        };

    public static Type GetValueType(StockCanonicalField field) => field switch
    {
        StockCanonicalField.ExpiredDate => typeof(DateTime),
        StockCanonicalField.ProductPcsInCtn or
        StockCanonicalField.ProductNetto or
        StockCanonicalField.PriceUnit or
        StockCanonicalField.SmallestUnitQty or
        StockCanonicalField.SaldoAwal or
        StockCanonicalField.SaldoMasukPO or
        StockCanonicalField.SaldoAkhir => typeof(decimal),
        _ => typeof(string)
    };

    /// <summary>
    /// Human readable name of the value type, shown next to each canonical field on the mapping screen so
    /// an administrator can see what a source column has to contain before picking it.
    /// </summary>
    public static string GetValueTypeLabel(StockCanonicalField field)
    {
        var type = GetValueType(field);
        if (type == typeof(int)) return "Whole number";
        if (type == typeof(decimal)) return "Number";
        if (type == typeof(DateTime)) return "Date";
        return "Text";
    }

    public static int? GetMaxLength(StockCanonicalField field) => field switch
    {
        StockCanonicalField.ProductId => 100,
        StockCanonicalField.ProductName => 200,
        StockCanonicalField.ProductPackaging => 100,
        StockCanonicalField.ProductUnit => 50,
        StockCanonicalField.SmallestUnit => 50,
        StockCanonicalField.BatchNumber => 100,
        _ => null
    };
}
