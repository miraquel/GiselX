using GiselX.Domain;
using GiselX.Service;

namespace GiselX.Tests;

public sealed class StockAreaMatcherTests
{
    private static StockAreaMatcher Matcher(params StockArea[] areas) => new(areas, "PT Primatrans");

    private static StockArea[] Default() =>
    [
        new StockArea { Id = 1, Name = "Medan", Aliases = "MDN; Gdg Medan" },
        new StockArea { Id = 2, Name = "Binjai" }
    ];

    [Fact]
    public void Name_matches_ignoring_case_and_spacing()
    {
        var matcher = Matcher(Default());
        Assert.Equal(1, matcher.Resolve(" medan ").AreaId);
        Assert.Equal(1, matcher.Resolve("MEDAN").AreaId);
    }

    [Fact]
    public void Alias_matches()
    {
        var matcher = Matcher(Default());
        Assert.Equal(1, matcher.Resolve("gdg  medan").AreaId);
        Assert.Equal(1, matcher.Resolve("mdn").AreaId);
    }

    [Fact]
    public void Unregistered_text_is_an_error_naming_text_and_company()
    {
        var result = Matcher(Default()).Resolve("Tebing");
        Assert.Null(result.AreaId);
        Assert.Equal("Area 'Tebing' is not registered for PT Primatrans.", result.Error);
    }

    [Fact]
    public void Blank_is_an_error()
    {
        var result = Matcher(Default()).Resolve("  ");
        Assert.Null(result.AreaId);
        Assert.Equal("Area is empty.", result.Error);
    }

    [Fact]
    public void Text_matching_two_areas_is_an_error_not_a_guess()
    {
        var result = Matcher(
            new StockArea { Id = 1, Name = "Medan", Aliases = "Sumut" },
            new StockArea { Id = 2, Name = "Binjai", Aliases = "sumut" }).Resolve("SUMUT");
        Assert.Null(result.AreaId);
        Assert.Contains("more than one area", result.Error);
    }

    [Fact]
    public void Inactive_area_never_resolves()
    {
        var result = Matcher(new StockArea { Id = 1, Name = "Medan", IsActive = false }).Resolve("Medan");
        Assert.Null(result.AreaId);
        Assert.Contains("not registered", result.Error);
    }

    [Fact]
    public void SplitList_normalises_drops_blanks_and_dedupes_keeping_first_spelling()
    {
        Assert.Equal(["MDN", "Gdg Medan"], StockTextNormalizer.SplitList(" MDN ;; mdn; Gdg   Medan ;"));
        Assert.Empty(StockTextNormalizer.SplitList(null));
    }
}
