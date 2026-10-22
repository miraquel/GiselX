using GiselX.Domain;

namespace GiselX.Service;

public static class StockCanonicalFieldCatalog
{
    public static IReadOnlySet<StockCanonicalField> RequiredFields { get; } =
        new HashSet<StockCanonicalField>
        {
            StockCanonicalField.ProductId,
            StockCanonicalField.ProductName
        };

    /// <summary>
    /// The stock quantity: cartons, pieces, or both. Distributors' files carry it as one column or as
    /// two, so a template must map at least one of these rather than each of them.
    /// </summary>
    public static IReadOnlySet<StockCanonicalField> QuantityFields { get; } =
        new HashSet<StockCanonicalField> { StockCanonicalField.QtyCarton, StockCanonicalField.QtyPcs };

    /// <summary>
    /// Opening balance, incoming PO and closing balance. Still read and stored for templates that map
    /// them, but no longer offered: PT. GMK reads the quantity in cartons and pieces instead.
    /// </summary>
    public static IReadOnlySet<StockCanonicalField> LegacyFields { get; } =
        new HashSet<StockCanonicalField>
        {
            StockCanonicalField.SaldoAwal, StockCanonicalField.SaldoMasukPO, StockCanonicalField.SaldoAkhir
        };

    /// <summary>The fields the template editor offers, in enum order.</summary>
    public static IReadOnlyList<StockCanonicalField> OfferedFields { get; } =
        Enum.GetValues<StockCanonicalField>().Where(field => !LegacyFields.Contains(field)).ToList();

    /// <summary>Quantity cells read as zero when blank: a distributor leaves the cell empty for no stock.</summary>
    public static IReadOnlySet<StockCanonicalField> ZeroWhenBlankFields { get; } =
        new HashSet<StockCanonicalField>
        {
            StockCanonicalField.QtyCarton, StockCanonicalField.QtyPcs, StockCanonicalField.SaldoAkhir
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
        StockCanonicalField.SaldoAkhir or
        StockCanonicalField.QtyCarton or
        StockCanonicalField.QtyPcs => typeof(decimal),
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
        StockCanonicalField.Area => 200,
        StockCanonicalField.Condition => 200,
        _ => null
    };
}
