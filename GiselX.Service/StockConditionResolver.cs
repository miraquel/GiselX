using GiselX.Domain;

namespace GiselX.Service;

public static class StockConditionResolver
{
    public static (StockCondition? Condition, string? Error) Resolve(
        string? cellText, string? goodValues, string? badValues, string templateName)
    {
        var text = StockTextNormalizer.Normalize(cellText);
        if (text.Length == 0)
        {
            return (null, "Condition is empty.");
        }

        var isGood = StockTextNormalizer.SplitList(goodValues).Contains(text, StringComparer.OrdinalIgnoreCase);
        var isBad = StockTextNormalizer.SplitList(badValues).Contains(text, StringComparer.OrdinalIgnoreCase);

        if (isGood && isBad)
        {
            return (null, $"Condition '{text}' is both a Good and a Bad value of template {templateName}.");
        }

        if (isGood)
        {
            return (StockCondition.Good, null);
        }

        if (isBad)
        {
            return (StockCondition.Bad, null);
        }

        return (null, $"Condition '{text}' is neither a Good nor a Bad value of template {templateName}.");
    }
}
