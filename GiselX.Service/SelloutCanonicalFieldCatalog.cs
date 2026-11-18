using GiselX.Domain;

namespace GiselX.Service;

public static class SelloutCanonicalFieldCatalog
{
    public static IReadOnlySet<SelloutCanonicalField> RequiredFields { get; } =
        new HashSet<SelloutCanonicalField>
        {
            SelloutCanonicalField.CustomerId,
            SelloutCanonicalField.CustomerName,
            SelloutCanonicalField.ProductId,
            SelloutCanonicalField.ProductName,
            SelloutCanonicalField.InvoiceNo,
            SelloutCanonicalField.InvoiceDate,
            SelloutCanonicalField.InvoiceQty,
            SelloutCanonicalField.InvoiceUnit
        };

    public static Type GetValueType(SelloutCanonicalField field) => field switch
    {
        SelloutCanonicalField.ExpiredDate or
        SelloutCanonicalField.InvoiceDate or
        SelloutCanonicalField.ShipDate => typeof(DateTime),
        SelloutCanonicalField.ProductPcsInCtn or
        SelloutCanonicalField.InvoiceQty or
        SelloutCanonicalField.UnitPrice or
        SelloutCanonicalField.GrossValue or
        SelloutCanonicalField.DiscountValue or
        SelloutCanonicalField.DiscountPct or
        SelloutCanonicalField.NetValue or
        SelloutCanonicalField.InKg => typeof(decimal),
        _ => typeof(string)
    };

    public static int? GetMaxLength(SelloutCanonicalField field) => field switch
    {
        SelloutCanonicalField.CustomerId => 100,
        SelloutCanonicalField.CustomerName => 200,
        SelloutCanonicalField.CustomerAlias => 200,
        SelloutCanonicalField.CustomerAddress => 500,
        SelloutCanonicalField.ProductId => 100,
        SelloutCanonicalField.ProductBrand => 200,
        SelloutCanonicalField.ProductName => 200,
        SelloutCanonicalField.ProductPackaging => 100,
        SelloutCanonicalField.BatchNumber => 100,
        SelloutCanonicalField.InvoiceNo => 100,
        SelloutCanonicalField.InvoiceUnit => 50,
        SelloutCanonicalField.GeisaPOId => 100,
        SelloutCanonicalField.SalesmanNameGMK => 200,
        _ => null
    };

    /// <summary>
    /// The explanation shown when hovering the field on the mapping screen: what the column means, the
    /// rules an upload applies to it, its format and an example. Empty only for a field nobody described,
    /// which <c>SelloutCanonicalFieldCatalogTests</c> refuses.
    /// </summary>
    public static string GetHint(SelloutCanonicalField field) =>
        Hints.TryGetValue(field, out var hint)
            ? CanonicalFieldHint.Compose(hint.Description, GetValueType(field), GetMaxLength(field), hint.Example)
            : string.Empty;

    private static readonly IReadOnlyDictionary<SelloutCanonicalField, (string Description, string? Example)> Hints =
        new Dictionary<SelloutCanonicalField, (string, string?)>
        {
            [SelloutCanonicalField.CustomerId] = ("Kode outlet/pelanggan di sistem distributor.", "OUT-00123"),
            [SelloutCanonicalField.CustomerName] = ("Nama outlet/pelanggan.", "Toko Sumber Rejeki"),
            [SelloutCanonicalField.CustomerAlias] = ("Nama lain outlet, mis. nama dagang atau nama yang biasa dipakai.", "TK Rejeki"),
            [SelloutCanonicalField.CustomerAddress] = ("Alamat outlet.", "Jl. Merdeka No. 10, Surabaya"),
            [SelloutCanonicalField.ProductId] = ("Kode barang di sistem distributor. Bila Item Mapping perusahaan aktif, kode ini diterjemahkan ke barang GMK.", "BRG-0451"),
            [SelloutCanonicalField.ProductBrand] = ("Merek barang.", null),
            [SelloutCanonicalField.ProductName] = ("Nama barang di sistem distributor. Ikut diterjemahkan bila Item Mapping perusahaan aktif.", null),
            [SelloutCanonicalField.ProductPackaging] = ("Kemasan barang.", "Sak 25 kg"),
            [SelloutCanonicalField.ProductPcsInCtn] = ("Jumlah pcs dalam satu karton.", "12"),
            [SelloutCanonicalField.BatchNumber] = ("Nomor batch produksi.", null),
            [SelloutCanonicalField.ExpiredDate] = ("Tanggal kedaluwarsa barang.", null),
            [SelloutCanonicalField.InvoiceNo] = ("Nomor faktur penjualan.", "INV/2026/11/0001"),
            [SelloutCanonicalField.InvoiceDate] = ("Tanggal faktur. Menentukan minggu laporan, dan rentang tanggal faktur yang digantikan saat upload ulang.", null),
            [SelloutCanonicalField.InvoiceQty] = ("Jumlah barang terjual pada baris faktur, dalam satuan InvoiceUnit. Tidak boleh 0; nilai negatif (retur) diterima.", "24"),
            [SelloutCanonicalField.InvoiceUnit] = ("Satuan dari InvoiceQty.", "CTN"),
            [SelloutCanonicalField.UnitPrice] = ("Harga per satuan InvoiceUnit.", "185000"),
            [SelloutCanonicalField.GrossValue] = ("Nilai penjualan sebelum diskon. Bila tidak dipetakan atau kosong, ringkasan upload menghitungnya dari InvoiceQty × UnitPrice.", null),
            [SelloutCanonicalField.DiscountValue] = ("Nilai diskon dalam rupiah.", null),
            [SelloutCanonicalField.DiscountPct] = ("Persentase diskon. Isi 10 untuk diskon 10%.", "10"),
            [SelloutCanonicalField.NetValue] = ("Nilai penjualan setelah diskon. Bila tidak dipetakan atau kosong, ringkasan upload menghitungnya dari GrossValue − DiscountValue.", null),
            [SelloutCanonicalField.InKg] = ("Volume penjualan dalam kilogram.", "250"),
            [SelloutCanonicalField.GeisaPOId] = ("Nomor PO Geisa.", null),
            [SelloutCanonicalField.ShipDate] = ("Tanggal barang dikirim ke outlet.", null),
            [SelloutCanonicalField.SalesmanNameGMK] = ("Nama salesman GMK.", null),
        };
}
