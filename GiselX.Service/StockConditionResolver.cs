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
            return (null, "Kolom Condition kosong. Isi kondisi stok (Good/Bad) di file lalu upload ulang.");
        }

        var isGood = StockTextNormalizer.SplitList(goodValues).Contains(text, StringComparer.OrdinalIgnoreCase);
        var isBad = StockTextNormalizer.SplitList(badValues).Contains(text, StringComparer.OrdinalIgnoreCase);

        if (isGood && isBad)
        {
            return (null, $"Nilai Condition '{text}' terdaftar sebagai Good sekaligus Bad di template {templateName}. Admin PT. GMK perlu memperbaiki template.");
        }

        if (isGood)
        {
            return (StockCondition.Good, null);
        }

        if (isBad)
        {
            return (StockCondition.Bad, null);
        }

        return (null, $"Nilai Condition '{text}' tidak dikenali template {templateName}. Gunakan nilai Good/Bad yang biasa, atau minta admin PT. GMK menambahkannya ke template.");
    }
}
