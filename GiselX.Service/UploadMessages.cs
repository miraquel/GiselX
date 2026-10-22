namespace GiselX.Service;

/// <summary>
/// Wording shared by the Stock and Sellout upload pipelines. Distributors read these messages, so they
/// are in Indonesian and, where the fix is not theirs to make, say that PT. GMK has to act.
/// </summary>
internal static class UploadMessages
{
    internal const string ContactGmk = "Hubungi admin PT. GMK.";
    internal const string NoDataRows = "File tidak berisi baris data.";
    internal const string EmptyFile = "File kosong.";
    internal const string ValueRequired = "Wajib diisi.";
    internal const string MustNotBeNegative = "Tidak boleh negatif.";

    internal static string FileTooLarge(long maximumBytes) =>
        $"Ukuran file melebihi batas {maximumBytes / 1048576d:0.#} MB.";

    internal static string TooLong(int limit) => $"Melebihi panjang maksimal {limit} karakter.";

    internal static string NotAValid(Type valueType) => valueType == typeof(DateTime)
        ? "Bukan tanggal yang valid."
        : valueType == typeof(decimal) || valueType == typeof(int) || valueType == typeof(double)
            ? "Bukan angka yang valid."
            : "Nilai tidak valid.";
}
