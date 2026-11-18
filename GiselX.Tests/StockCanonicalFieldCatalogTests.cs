using GiselX.Domain;
using GiselX.Service;

namespace GiselX.Tests;

public sealed class StockCanonicalFieldCatalogTests
{
    [Fact]
    public void Required_fields_are_product_identity()
    {
        // The quantity is required as "at least one of QtyCarton and QtyPcs", checked by the template service.
        Assert.Equal(
            [StockCanonicalField.ProductId, StockCanonicalField.ProductName],
            StockCanonicalFieldCatalog.RequiredFields.Order());
    }

    [Theory]
    [InlineData(StockCanonicalField.ProductPcsInCtn, typeof(decimal))]
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

    [Fact]
    public void SmallestUnit_fields_are_appended_after_the_existing_catalog()
    {
        // Appended so persisted StockTemplateColumn.CanonicalField values keep their meaning.
        Assert.Equal(12, (int)StockCanonicalField.SmallestUnit);
        Assert.Equal(13, (int)StockCanonicalField.SmallestUnitQty);
    }

    [Fact]
    public void SmallestUnit_is_optional_text_capped_at_fifty_characters()
    {
        Assert.Equal(typeof(string), StockCanonicalFieldCatalog.GetValueType(StockCanonicalField.SmallestUnit));
        Assert.Equal(50, StockCanonicalFieldCatalog.GetMaxLength(StockCanonicalField.SmallestUnit));
        Assert.DoesNotContain(StockCanonicalField.SmallestUnit, StockCanonicalFieldCatalog.RequiredFields);
    }

    [Fact]
    public void SmallestUnitQty_is_optional_and_decimal()
    {
        Assert.Equal(typeof(decimal), StockCanonicalFieldCatalog.GetValueType(StockCanonicalField.SmallestUnitQty));
        Assert.Null(StockCanonicalFieldCatalog.GetMaxLength(StockCanonicalField.SmallestUnitQty));
        Assert.DoesNotContain(StockCanonicalField.SmallestUnitQty, StockCanonicalFieldCatalog.RequiredFields);
    }

    [Theory]
    [InlineData(StockCanonicalField.ProductId, "Text")]
    [InlineData(StockCanonicalField.ProductPcsInCtn, "Number")]
    [InlineData(StockCanonicalField.SaldoAkhir, "Number")]
    [InlineData(StockCanonicalField.ExpiredDate, "Date")]
    public void Catalog_exposes_a_readable_value_type_label(StockCanonicalField field, string expected) =>
        Assert.Equal(expected, StockCanonicalFieldCatalog.GetValueTypeLabel(field));

    public static TheoryData<StockCanonicalField> AllFields() => new(Enum.GetValues<StockCanonicalField>());

    [Theory]
    [MemberData(nameof(AllFields))]
    public void Every_field_has_a_mapping_screen_hint(StockCanonicalField field)
    {
        // A field added to the enum without a hint would show an empty tooltip on the mapping screen.
        var hint = StockCanonicalFieldCatalog.GetHint(field);

        Assert.False(string.IsNullOrWhiteSpace(hint));
        Assert.Contains("Format: ", hint);
    }

    [Theory]
    [InlineData(StockCanonicalField.ProductId, "Format: Teks, maks. 100 karakter")]
    [InlineData(StockCanonicalField.QtyCarton, "Format: Angka")]
    [InlineData(StockCanonicalField.ExpiredDate, "Format: Tanggal")]
    public void Hint_format_line_follows_the_parser_rules(StockCanonicalField field, string expected) =>
        Assert.Contains(expected, StockCanonicalFieldCatalog.GetHint(field).Split('\n'));
}
