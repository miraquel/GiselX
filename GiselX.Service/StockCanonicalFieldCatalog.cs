using GiselX.Domain;

namespace GiselX.Service;

public static class StockCanonicalFieldCatalog
{
    public static IReadOnlySet<StockCanonicalField> RequiredFields { get; } =
        new HashSet<StockCanonicalField>
        {
            StockCanonicalField.ProductId,
            StockCanonicalField.ProductName
        };

    /// <summary>
    /// The stock quantity: cartons, pieces, or both. Distributors' files carry it as one column or as
    /// two, so a template must map at least one of these rather than each of them.
    /// </summary>
    public static IReadOnlySet<StockCanonicalField> QuantityFields { get; } =
        new HashSet<StockCanonicalField> { StockCanonicalField.QtyCarton, StockCanonicalField.QtyPcs };

    /// <summary>
    /// Opening balance, incoming PO and closing balance. Still read and stored for templates that map
    /// them, but no longer offered: PT. GMK reads the quantity in cartons and pieces instead.
    /// </summary>
    public static IReadOnlySet<StockCanonicalField> LegacyFields { get; } =
        new HashSet<StockCanonicalField>
        {
            StockCanonicalField.SaldoAwal, StockCanonicalField.SaldoMasukPO, StockCanonicalField.SaldoAkhir
        };

    /// <summary>The fields the template editor offers, in enum order.</summary>
    public static IReadOnlyList<StockCanonicalField> OfferedFields { get; } =
        Enum.GetValues<StockCanonicalField>().Where(field => !LegacyFields.Contains(field)).ToList();

    /// <summary>Quantity cells read as zero when blank: a distributor leaves the cell empty for no stock.</summary>
    public static IReadOnlySet<StockCanonicalField> ZeroWhenBlankFields { get; } =
        new HashSet<StockCanonicalField>
        {
            StockCanonicalField.QtyCarton, StockCanonicalField.QtyPcs, StockCanonicalField.SaldoAkhir
        };

    public static Type GetValueType(StockCanonicalField field) => field switch
    {
        StockCanonicalField.ExpiredDate => typeof(DateTime),
        StockCanonicalField.ProductPcsInCtn or
        StockCanonicalField.ProductNetto or
        StockCanonicalField.PriceUnit or
        StockCanonicalField.SmallestUnitQty or
        StockCanonicalField.SaldoAwal or
        StockCanonicalField.SaldoMasukPO or
        StockCanonicalField.SaldoAkhir or
        StockCanonicalField.QtyCarton or
        StockCanonicalField.QtyPcs => typeof(decimal),
        _ => typeof(string)
    };

    /// <summary>
    /// Human readable name of the value type, shown next to each canonical field on the mapping screen so
    /// an administrator can see what a source column has to contain before picking it.
    /// </summary>
    public static string GetValueTypeLabel(StockCanonicalField field)
    {
        var type = GetValueType(field);
        if (type == typeof(int)) return "Whole number";
        if (type == typeof(decimal)) return "Number";
        if (type == typeof(DateTime)) return "Date";
        return "Text";
    }

    public static int? GetMaxLength(StockCanonicalField field) => field switch
    {
        StockCanonicalField.ProductId => 100,
        StockCanonicalField.ProductName => 200,
        StockCanonicalField.ProductPackaging => 100,
        StockCanonicalField.ProductUnit => 50,
        StockCanonicalField.SmallestUnit => 50,
        StockCanonicalField.BatchNumber => 100,
        StockCanonicalField.Area => 200,
        StockCanonicalField.Condition => 200,
        _ => null
    };

    /// <summary>
    /// The explanation shown when hovering the field on the mapping screen: what the column means, the
    /// rules an upload applies to it, its format and an example. Empty only for a field nobody described,
    /// which <c>StockCanonicalFieldCatalogTests</c> refuses.
    /// </summary>
    public static string GetHint(StockCanonicalField field) =>
        Hints.TryGetValue(field, out var hint)
            ? CanonicalFieldHint.Compose(hint.Description, GetValueType(field), GetMaxLength(field), hint.Example)
            : string.Empty;

    private const string QuantityRule = "Petakan QtyCarton, QtyPcs, atau keduanya. Sel kosong dibaca 0; tidak boleh negatif.";

    private static readonly IReadOnlyDictionary<StockCanonicalField, (string Description, string? Example)> Hints =
        new Dictionary<StockCanonicalField, (string, string?)>
        {
            [StockCanonicalField.ProductId] = ("Kode barang di sistem distributor. Bila Item Mapping perusahaan aktif, kode ini diterjemahkan ke barang GMK.", "BRG-0451"),
            [StockCanonicalField.ProductName] = ("Nama barang di sistem distributor. Ikut diterjemahkan bila Item Mapping perusahaan aktif.", null),
            [StockCanonicalField.ProductPackaging] = ("Kemasan barang.", "Sak 25 kg"),
            [StockCanonicalField.ProductPcsInCtn] = ("Jumlah pcs dalam satu karton.", "12"),
            [StockCanonicalField.ProductNetto] = ("Berat bersih barang.", "25"),
            [StockCanonicalField.ProductUnit] = ("Satuan barang.", "CTN"),
            [StockCanonicalField.SaldoAwal] = ("Saldo awal. Kolom lama: masih dibaca untuk template lama, tidak lagi ditawarkan.", null),
            [StockCanonicalField.SaldoMasukPO] = ("Barang masuk dari PO. Kolom lama: masih dibaca untuk template lama, tidak lagi ditawarkan.", null),
            [StockCanonicalField.SaldoAkhir] = ("Saldo akhir. Kolom lama: masih dibaca untuk template lama, tidak lagi ditawarkan. Sel kosong dibaca 0; tidak boleh negatif.", null),
            [StockCanonicalField.BatchNumber] = ("Nomor batch produksi.", null),
            [StockCanonicalField.ExpiredDate] = ("Tanggal kedaluwarsa barang.", null),
            [StockCanonicalField.PriceUnit] = ("Harga per satuan. Tidak boleh negatif.", "185000"),
            [StockCanonicalField.SmallestUnit] = ("Satuan terkecil barang.", "PCS"),
            [StockCanonicalField.SmallestUnitQty] = ("Jumlah stok dalam satuan terkecil. Tidak boleh negatif.", "240"),
            [StockCanonicalField.Area] = ("Nama area/gudang stok, dicocokkan dengan nama atau alias area yang terdaftar untuk perusahaan. Wajib dipetakan bila perusahaan punya area; bila tidak, kolom ini diabaikan.", "Gudang Utama"),
            [StockCanonicalField.Condition] = ("Kondisi stok. Isi sel diterjemahkan menjadi Good atau Bad lewat isian Values meaning Good/Bad stock di bawah tabel. Sel kosong membuat baris ditolak.", "Baik; Rusak"),
            [StockCanonicalField.QtyCarton] = ($"Jumlah stok dalam karton. {QuantityRule}", "120"),
            [StockCanonicalField.QtyPcs] = ($"Jumlah stok dalam pcs. {QuantityRule}", "6"),
        };
}
