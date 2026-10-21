using GiselX.Domain;
using GiselX.Service;

namespace GiselX.Tests;

/// <summary>
/// Distributor workbooks write invoice dates in whatever the exporting report produced. A whole month of
/// sellout once failed to upload because every "21 Agu 2026" cell carried non-breaking spaces, leaving a
/// single English-formatted row valid. These pin the shapes that must load.
/// </summary>
public sealed class WorkbookDateTextParserTests
{
    [Theory]
    // Indonesian abbreviated and full month names, as the RAW DATA workbooks write them.
    [InlineData("21 Agu 2026", 2026, 8, 21)]
    [InlineData("19 Agustus 2026", 2026, 8, 19)]
    [InlineData("1 Mei 2026", 2026, 5, 1)]
    [InlineData("30 Des 2026", 2026, 12, 30)]
    // Local abbreviations no culture data knows.
    [InlineData("21 Agt 2026", 2026, 8, 21)]
    [InlineData("21 Agust 2026", 2026, 8, 21)]
    [InlineData("3 Nop 2026", 2026, 11, 3)]
    [InlineData("28 Peb 2026", 2026, 2, 28)]
    [InlineData("5 Mrt 2026", 2026, 3, 5)]
    // English, including the two-digit year form that was the only row to survive.
    [InlineData("27-Aug-26", 2026, 8, 27)]
    [InlineData("27 September 2026", 2026, 9, 27)]
    // Numeric forms.
    [InlineData("27/08/2026", 2026, 8, 27)]
    [InlineData("27-08-2026", 2026, 8, 27)]
    [InlineData("27.08.2026", 2026, 8, 27)]
    [InlineData("2026-08-27", 2026, 8, 27)]
    [InlineData("31/12/99", 1999, 12, 31)]
    // Excel date cells arrive as OLE Automation serials.
    [InlineData("46261", 2026, 8, 27)]
    // Separators the exporter emits instead of ordinary spaces.
    [InlineData("  21   Agu   2026  ", 2026, 8, 21)]
    [InlineData("21 agu 2026", 2026, 8, 21)]
    [InlineData("21 AGU 2026", 2026, 8, 21)]
    // A trailing time of day is not part of a date field.
    [InlineData("27/08/2026 00:00:00", 2026, 8, 27)]
    public void Parser_reads_the_date_shapes_distributor_workbooks_contain(string source, int year, int month, int day)
    {
        Assert.True(WorkbookDateTextParser.TryParse(source, out var value), $"'{source}' should parse.");
        Assert.Equal(new DateTime(year, month, day), value.Date);
    }

    /// <summary>
    /// A stock form is filled in by hand, so the expiry cell often carries a note about the batch it
    /// refers to - sometimes bracketed, just as often not. The date leads and the note follows, so the
    /// date is read and the note dropped.
    /// </summary>
    [Theory]
    [InlineData("09/02/2027 (BS)", 2027, 2, 9)]
    [InlineData("20/02/2027 (163)", 2027, 2, 20)]
    [InlineData("31/12/2026 (7 ctn 7 pcs)", 2026, 12, 31)]
    [InlineData("27/08/2027 (10ctn)", 2027, 8, 27)]
    [InlineData("16/01/2027 (1 ctn)", 2027, 1, 16)]
    // The same notes without brackets.
    [InlineData("09/02/2027 BS", 2027, 2, 9)]
    [InlineData("27/08/2027 10ctn", 2027, 8, 27)]
    [InlineData("21 Agu 2026 sisa 3 ctn", 2026, 8, 21)]
    [InlineData("31-12-2026 batch 163", 2026, 12, 31)]
    // A date written inside brackets on its own.
    [InlineData("(31/12/2026)", 2026, 12, 31)]
    public void Parser_reads_a_date_that_carries_a_handwritten_note(string source, int year, int month, int day)
    {
        Assert.True(WorkbookDateTextParser.TryParse(source, out var value), $"'{source}' should parse.");
        Assert.Equal(new DateTime(year, month, day), value.Date);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bukan tanggal")]
    [InlineData("SI26032839")]
    [InlineData("32/13/2026")]
    [InlineData("31 Feb 2026")]
    [InlineData("21 Agu")]
    // A note is only dropped when a date leads it. Text that merely contains one is not mined.
    [InlineData("Mengetahui")]
    [InlineData("Colatta Compound 12 x 1 kg")]
    [InlineData("Haan S-Mix Soft Chocolate 8X1.5Kg")]
    [InlineData("4 x 5 kg")]
    [InlineData("stock opname 31/12/2026")]
    public void Parser_rejects_text_that_is_not_a_date(string source)
    {
        Assert.False(WorkbookDateTextParser.TryParse(source, out _), $"'{source}' should not parse.");
    }

    /// <summary>
    /// The actual defect: report exporters separate the parts with non-breaking and thin spaces, which
    /// look like ordinary spaces on screen and which no culture's date parser accepts.
    /// </summary>
    [Fact]
    public void Parser_reads_dates_separated_by_exporter_whitespace()
    {
        foreach (var separator in new[] { (char)0x00A0, (char)0x202F, (char)0x2007, (char)0x2009 })
        {
            var source = $"21{separator}Agu{separator}2026";

            Assert.True(WorkbookDateTextParser.TryParse(source, out var value),
                $"A date separated by U+{(int)separator:X4} should parse.");
            Assert.Equal(new DateTime(2026, 8, 21), value.Date);
        }

        var zeroWidthWrapped = $"{(char)0x200B}21 Agu 2026{(char)0xFEFF}";

        Assert.True(WorkbookDateTextParser.TryParse(zeroWidthWrapped, out var trimmed));
        Assert.Equal(new DateTime(2026, 8, 21), trimmed.Date);
    }

    [Fact]
    public void Parser_rejects_a_date_without_a_year_instead_of_assuming_the_current_one()
    {
        Assert.False(WorkbookDateTextParser.TryParse("21 Agu", out _));
        Assert.False(WorkbookDateTextParser.TryParse("21/08", out _));
    }

    [Fact]
    public void Parser_reads_month_first_numeric_dates_when_day_first_is_impossible()
    {
        Assert.True(WorkbookDateTextParser.TryParse("08/27/2026", out var value));
        Assert.Equal(new DateTime(2026, 8, 27), value.Date);
    }

    [Fact]
    public void Sellout_invoice_date_accepts_the_indonesian_text_form()
    {
        Assert.True(SelloutCellValueConverter.TryConvert("21 Agu 2026",
            SelloutCanonicalField.InvoiceDate, out var value));
        Assert.Equal(new DateTime(2026, 8, 21), Assert.IsType<DateTime>(value).Date);
    }

    [Fact]
    public void Stock_expired_date_accepts_the_indonesian_text_form()
    {
        Assert.True(StockCellValueConverter.TryConvert("21 Agt 2026",
            StockCanonicalField.ExpiredDate, out var value));
        Assert.Equal(new DateTime(2026, 8, 21), Assert.IsType<DateTime>(value).Date);
    }
}
