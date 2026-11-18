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

    public static TheoryData<SelloutCanonicalField> AllFields() => new(Enum.GetValues<SelloutCanonicalField>());

    [Theory]
    [MemberData(nameof(AllFields))]
    public void Every_field_has_a_mapping_screen_hint(SelloutCanonicalField field)
    {
        // A field added to the enum without a hint would show an empty tooltip on the mapping screen.
        var hint = SelloutCanonicalFieldCatalog.GetHint(field);

        Assert.False(string.IsNullOrWhiteSpace(hint));
        Assert.Contains("Format: ", hint);
    }

    [Theory]
    [InlineData(SelloutCanonicalField.InvoiceUnit, "Format: Teks, maks. 50 karakter")]
    [InlineData(SelloutCanonicalField.InvoiceQty, "Format: Angka")]
    [InlineData(SelloutCanonicalField.InvoiceDate, "Format: Tanggal")]
    [InlineData(SelloutCanonicalField.DiscountPct, "Contoh: 10")]
    public void Hint_lines_follow_the_parser_rules(SelloutCanonicalField field, string expected) =>
        Assert.Contains(expected, SelloutCanonicalFieldCatalog.GetHint(field).Split('\n'));
}
