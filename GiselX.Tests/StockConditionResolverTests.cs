using GiselX.Domain;
using GiselX.Service;

namespace GiselX.Tests;

public sealed class StockConditionResolverTests
{
    private static (StockCondition? Condition, string? Error) Resolve(string? text) =>
        StockConditionResolver.Resolve(text, "GOOD;GS", "BAD;BS;BAD STOCK", "Primatrans Stock");

    [Fact]
    public void Good_and_bad_values_resolve_ignoring_case_and_spacing()
    {
        Assert.Equal(StockCondition.Good, Resolve(" gs ").Condition);
        Assert.Equal(StockCondition.Bad, Resolve("bad  stock").Condition);
    }

    [Fact]
    public void Unknown_value_is_an_error_quoting_it()
    {
        var result = Resolve("ED");
        Assert.Null(result.Condition);
        Assert.Equal("Condition 'ED' is neither a Good nor a Bad value of template Primatrans Stock.", result.Error);
    }

    [Fact]
    public void Blank_is_an_error_never_good()
    {
        var result = Resolve("");
        Assert.Null(result.Condition);
        Assert.Equal("Condition is empty.", result.Error);
    }

    [Fact]
    public void Value_in_both_lists_is_an_error_not_a_guess()
    {
        var result = StockConditionResolver.Resolve("X", "X", "x", "T");
        Assert.Null(result.Condition);
        Assert.Contains("both", result.Error);
    }
}
