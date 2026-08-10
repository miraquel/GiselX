using GiselX.Domain;
using GiselX.Service;

namespace GiselX.Tests;

public sealed class StockCanonicalFieldCatalogTests
{
    [Fact]
    public void Required_fields_are_product_identity_and_closing_balance()
    {
        Assert.Equal(
            [StockCanonicalField.ProductId, StockCanonicalField.ProductName, StockCanonicalField.SaldoAkhir],
            StockCanonicalFieldCatalog.RequiredFields.Order());
    }

    [Theory]
    [InlineData(StockCanonicalField.ProductPcsInCtn, typeof(int))]
    [InlineData(StockCanonicalField.ProductNetto, typeof(decimal))]
    [InlineData(StockCanonicalField.SaldoAkhir, typeof(decimal))]
    [InlineData(StockCanonicalField.ExpiredDate, typeof(DateTime))]
    public void Catalog_exposes_strict_types(StockCanonicalField field, Type expected) =>
        Assert.Equal(expected, StockCanonicalFieldCatalog.GetValueType(field));

    [Fact]
    public void PriceUnit_is_appended_optional_and_decimal()
    {
        Assert.Equal(11, (int)StockCanonicalField.PriceUnit);
        Assert.Equal(typeof(decimal), StockCanonicalFieldCatalog.GetValueType(StockCanonicalField.PriceUnit));
        Assert.DoesNotContain(StockCanonicalField.PriceUnit, StockCanonicalFieldCatalog.RequiredFields);
    }
}
