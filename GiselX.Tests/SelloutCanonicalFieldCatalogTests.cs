using GiselX.Domain;
using GiselX.Service;

namespace GiselX.Tests;

public class SelloutCanonicalFieldCatalogTests
{
    [Fact]
    public void RequiredFields_AreExactlyTheApprovedCoreFields()
    {
        var expected = new HashSet<SelloutCanonicalField>
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

        Assert.Equal(expected, SelloutCanonicalFieldCatalog.RequiredFields.ToHashSet());
    }

    [Fact]
    public void Catalog_ContainsEveryCanonicalField()
    {
        Assert.All(Enum.GetValues<SelloutCanonicalField>(), field =>
            Assert.NotNull(SelloutCanonicalFieldCatalog.GetValueType(field)));
    }

    [Theory]
    [InlineData(SelloutCanonicalField.CustomerId, 100)]
    [InlineData(SelloutCanonicalField.CustomerAddress, 500)]
    [InlineData(SelloutCanonicalField.InvoiceUnit, 50)]
    [InlineData(SelloutCanonicalField.InvoiceQty, null)]
    public void MaxLengths_MatchCanonicalSalesSchema(SelloutCanonicalField field, int? expected)
    {
        Assert.Equal(expected, SelloutCanonicalFieldCatalog.GetMaxLength(field));
    }
}
