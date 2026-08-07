using GiselX.Domain;

namespace GiselX.Service;

public static class SelloutCanonicalFieldCatalog
{
    public static IReadOnlySet<SelloutCanonicalField> RequiredFields { get; } =
        new HashSet<SelloutCanonicalField>
        {
            SelloutCanonicalField.CustomerId,
            SelloutCanonicalField.CustomerName,
            SelloutCanonicalField.ProductId,
            SelloutCanonicalField.ProductName,
            SelloutCanonicalField.InvoiceNo,
            SelloutCanonicalField.InvoiceDate,
            SelloutCanonicalField.InvoiceQty,
            SelloutCanonicalField.InvoiceUnit
        };

    public static Type GetValueType(SelloutCanonicalField field) => field switch
    {
        SelloutCanonicalField.ProductPcsInCtn => typeof(int),
        SelloutCanonicalField.ExpiredDate or
        SelloutCanonicalField.InvoiceDate or
        SelloutCanonicalField.ShipDate => typeof(DateTime),
        SelloutCanonicalField.InvoiceQty or
        SelloutCanonicalField.UnitPrice or
        SelloutCanonicalField.GrossValue or
        SelloutCanonicalField.DiscountValue or
        SelloutCanonicalField.DiscountPct or
        SelloutCanonicalField.NetValue or
        SelloutCanonicalField.InKg => typeof(decimal),
        _ => typeof(string)
    };

    public static int? GetMaxLength(SelloutCanonicalField field) => field switch
    {
        SelloutCanonicalField.CustomerId => 100,
        SelloutCanonicalField.CustomerName => 200,
        SelloutCanonicalField.CustomerAlias => 200,
        SelloutCanonicalField.CustomerAddress => 500,
        SelloutCanonicalField.ProductId => 100,
        SelloutCanonicalField.ProductBrand => 200,
        SelloutCanonicalField.ProductName => 200,
        SelloutCanonicalField.ProductPackaging => 100,
        SelloutCanonicalField.BatchNumber => 100,
        SelloutCanonicalField.InvoiceNo => 100,
        SelloutCanonicalField.InvoiceUnit => 50,
        SelloutCanonicalField.GeisaPOId => 100,
        SelloutCanonicalField.SalesmanNameGMK => 200,
        _ => null
    };
}
