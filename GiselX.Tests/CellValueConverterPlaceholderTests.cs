using GiselX.Domain;
using GiselX.Service;

namespace GiselX.Tests;

/// <summary>
/// Distributor exports write a dash where a numeric or date cell has no value. Sellout uploads are
/// all-or-nothing, so treating that as a conversion failure rejected the entire workbook over rows
/// whose sales data was perfectly good.
/// </summary>
public sealed class CellValueConverterPlaceholderTests
{
    [Theory]
    [InlineData("-")]
    [InlineData("--")]
    [InlineData(" - ")]
    [InlineData("\u2013")]
    [InlineData("\u2014")]
    public void Sellout_reads_a_dash_in_a_date_cell_as_no_value(string source)
    {
        Assert.True(SelloutCellValueConverter.TryConvert(source, SelloutCanonicalField.ShipDate, out var value));
        Assert.Null(value);
    }

    [Theory]
    [InlineData(SelloutCanonicalField.InvoiceQty)]
    [InlineData(SelloutCanonicalField.UnitPrice)]
    [InlineData(SelloutCanonicalField.ProductPcsInCtn)]
    public void Sellout_reads_a_dash_in_a_numeric_cell_as_no_value(SelloutCanonicalField field)
    {
        Assert.True(SelloutCellValueConverter.TryConvert("-", field, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Sellout_keeps_a_dash_in_a_text_cell_verbatim()
    {
        Assert.True(SelloutCellValueConverter.TryConvert("-", SelloutCanonicalField.CustomerName, out var value));
        Assert.Equal("-", value);
    }

    [Theory]
    [InlineData("not a date")]
    [InlineData("-x-")]
    public void Sellout_still_rejects_a_value_that_is_not_merely_a_dash(string source)
    {
        Assert.False(SelloutCellValueConverter.TryConvert(source, SelloutCanonicalField.ShipDate, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Sellout_still_reads_a_real_date()
    {
        Assert.True(SelloutCellValueConverter.TryConvert("46237", SelloutCanonicalField.InvoiceDate, out var value));
        Assert.Equal(new DateTime(2026, 8, 3), value);
    }

    [Fact]
    public void Stock_reads_a_dash_in_a_date_cell_as_no_value()
    {
        Assert.True(StockCellValueConverter.TryConvert("-", StockCanonicalField.ExpiredDate, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Stock_reads_a_dash_in_a_numeric_cell_as_no_value()
    {
        Assert.True(StockCellValueConverter.TryConvert("-", StockCanonicalField.SaldoAkhir, out var value));
        Assert.Null(value);
    }

    [Fact]
    public void Stock_keeps_a_dash_in_a_text_cell_verbatim()
    {
        Assert.True(StockCellValueConverter.TryConvert("-", StockCanonicalField.BatchNumber, out var value));
        Assert.Equal("-", value);
    }
}
