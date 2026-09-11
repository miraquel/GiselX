using GiselX.Service;

namespace GiselX.Tests;

public class WorkbookHeaderNormalizerTests
{
    [Theory]
    [InlineData(" Invoice   No ", "INVOICE NO")]
    [InlineData("customer\tname", "CUSTOMER NAME")]
    public void Normalize_TrimsCollapsesWhitespaceAndIgnoresCase(string input, string expected)
    {
        Assert.Equal(expected, WorkbookHeaderNormalizer.Normalize(input));
    }

    [Fact]
    public void CreateSignature_IsOrderedAndDropsTrailingBlanks()
    {
        Assert.Equal("CUSTOMER ID\u001FINVOICE NO",
            WorkbookHeaderNormalizer.CreateSignature([" Customer Id ", "Invoice No", "", "  "]));
    }

    [Fact]
    public void CreateSignature_PreservesInternalBlankColumns()
    {
        Assert.Equal("CUSTOMER ID\u001F\u001FINVOICE NO",
            WorkbookHeaderNormalizer.CreateSignature(["Customer Id", "", "Invoice No"]));
    }

    [Fact]
    public void CreateDisplayHeaders_DeclaresBlankColumnsAndNumbersDuplicateHeaders()
    {
        var headers = WorkbookHeaderNormalizer.CreateDisplayHeaders([null, "Unit", " unit ", "UNIT"]);

        Assert.Equal(["Column 1", "Unit", "Unit 2", "Unit 3"], headers);
        Assert.Empty(WorkbookHeaderNormalizer.ValidateHeaders(headers));
    }

    [Fact]
    public void CreateDisplayHeaders_PreservesBlankMergedContinuationColumns()
    {
        var headers = WorkbookHeaderNormalizer.CreateDisplayHeaders(
            ["Invoice Date", "", "", "Product ID"],
            new HashSet<int> { 2, 3 });

        Assert.Equal(["Invoice Date", "", "", "Product ID"], headers);
    }

    [Fact]
    public void CreateDisplayHeaders_HandlesANaturalHeaderThatMatchesAGeneratedName()
    {
        var headers = WorkbookHeaderNormalizer.CreateDisplayHeaders(["Unit", "Unit", "Unit 2"]);

        Assert.Equal(["Unit", "Unit 2", "Unit 2 2"], headers);
        Assert.Equal(headers.Count, headers.Select(WorkbookHeaderNormalizer.Normalize).Distinct().Count());
    }
}
