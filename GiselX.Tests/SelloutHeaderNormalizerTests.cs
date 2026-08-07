using GiselX.Service;

namespace GiselX.Tests;

public class SelloutHeaderNormalizerTests
{
    [Theory]
    [InlineData(" Invoice   No ", "INVOICE NO")]
    [InlineData("customer\tname", "CUSTOMER NAME")]
    public void Normalize_TrimsCollapsesWhitespaceAndIgnoresCase(string input, string expected)
    {
        Assert.Equal(expected, SelloutHeaderNormalizer.Normalize(input));
    }

    [Fact]
    public void CreateSignature_IsOrderedAndDropsTrailingBlanks()
    {
        Assert.Equal("CUSTOMER ID\u001FINVOICE NO",
            SelloutHeaderNormalizer.CreateSignature([" Customer Id ", "Invoice No", "", "  "]));
    }

    [Fact]
    public void CreateSignature_PreservesInternalBlankColumns()
    {
        Assert.Equal("CUSTOMER ID\u001F\u001FINVOICE NO",
            SelloutHeaderNormalizer.CreateSignature(["Customer Id", "", "Invoice No"]));
    }

    [Fact]
    public void ValidateHeaders_RejectsDuplicateNormalizedHeaders()
    {
        var errors = SelloutHeaderNormalizer.ValidateHeaders(["Invoice No", " invoice   no "]);

        Assert.Single(errors);
        Assert.Contains("duplicate", errors[0], StringComparison.OrdinalIgnoreCase);
    }
}
