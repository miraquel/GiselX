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
        StockCanonicalField.ProductPcsInCtn => typeof(int),
        StockCanonicalField.ExpiredDate => typeof(DateTime),
        StockCanonicalField.ProductNetto or
        StockCanonicalField.PriceUnit or
        StockCanonicalField.SaldoAwal or
        StockCanonicalField.SaldoMasukPO or
        StockCanonicalField.SaldoAkhir => typeof(decimal),
        _ => typeof(string)
    };

    public static int? GetMaxLength(StockCanonicalField field) => field switch
    {
        StockCanonicalField.ProductId => 100,
        StockCanonicalField.ProductName => 200,
        StockCanonicalField.ProductPackaging => 100,
        StockCanonicalField.ProductUnit => 50,
        StockCanonicalField.BatchNumber => 100,
        _ => null
    };
}
