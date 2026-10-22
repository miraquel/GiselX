# Panduan Deploy GiselX

Panduan ini untuk operator yang memasang GiselX di server production (IIS + SQL Server). Detail
teknis tiap bagian ada di `CLAUDE.md`.

## 1. Konfigurasi wajib (`appsettings.json` atau environment variable)

Isi bagian berikut. Nilai sensitif sebaiknya diisi lewat environment variable, misalnya
`ConnectionStrings__DefaultConnection`, bukan langsung di file.

| Setting | Isi | Catatan |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | Connection string SQL Server | Hangfire memakai database yang sama. |
| `Database:MigrateOnStartup` | `true` (default) | Aplikasi menjalankan migration otomatis saat start. Isi `false` hanya kalau skema dipasang dengan script manual (bagian 3B). |
| `AdminSeed:Password` | Password admin pertama | Hanya dipakai ketika tabel user masih kosong. Isi lewat `AdminSeed__Password`. |
| `DataProtection:KeysPath` | Folder yang bisa ditulis oleh app pool, misalnya `D:\GiselX\keys` | **Wajib di IIS.** Tanpa ini password SMTP yang tersimpan bisa tidak terbaca lagi setelah restart. Setelah diisi pertama kali, masukkan ulang password SMTP di Admin → Email Settings. |
| `UploadArchive:Path` | Folder arsip file upload, misalnya `D:\GiselX\uploads` | Tanpa ini email notifikasi tidak menyertakan link download file. Cek di `/Admin/EmailTest`. |
| `Hangfire:DailyJobHour` | Jam kirim reminder, default `8` | Jam lokal server. |
| `Hangfire:TimeZone` | Kosong, atau mis. `SE Asia Standard Time` | Isi hanya kalau zona waktu server bukan WIB. |
| `SelloutUpload:MaxInvalidRowPercentage` | Default `5` | File Sellout ditolak seluruhnya bila persentase baris bermasalah ≥ nilai ini. |
| `SelloutUpload:MaxReplacementDays` | Default `45` | File Sellout ditolak bila rentang tanggal fakturnya lebih panjang (pelindung tanggal salah ketik). |

SMTP diatur di aplikasi (Admin → Email Settings), bukan di file. Bagian `Email` di file hanya cadangan.

## 2. Akun dan role

- Saat start pertama, aplikasi membuat role **Administrator** (semua permission) dan user admin (bila
  `AdminSeed:Password` diisi), serta role **Distributor** dengan permission lihat, upload dan download
  Sellout, Stock dan Service Level.
- User distributor diberi role **Distributor** dan dikaitkan ke company-nya. Hanya role dengan nama
  persis `Distributor` yang melihat kode produk versi distributor.
- Hanya Administrator yang bisa memberi atau mencabut role Administrator.

## 3. Memasang atau memperbarui skema database

Pilih **salah satu** cara.

### A. Migration otomatis (disarankan, wajib untuk database baru)

Biarkan `Database:MigrateOnStartup=true`. Saat aplikasi start, semua migration yang belum terpasang
dijalankan otomatis. Database **baru** harus memakai cara ini, karena beberapa migration awal (sebelum
Agustus 2026) tidak punya script manual.

### B. Script manual (untuk server yang tidak menjalankan migration)

Jalankan script di `GiselX.Web/DatabaseScripts/` yang namanya diawali id migration, **berurutan
menurut id**, hanya yang belum tercatat di tabel `dbo.__EFMigrationsHistory`. Setiap script aman
dijalankan ulang dan mencatat dirinya sendiri di tabel tersebut. Jalankan dengan
`sqlcmd -S <server> -d <database> -E -C -f 65001 -b -i <file>`.

Script terbaru untuk go-live November 2026:

1. `20261021142701_AddStockAreaAndCondition.sql`
2. `20261022020222_SelloutDateRangeReplacement.sql`
3. `20261022062547_AddCompanyLocation.sql`

## 4. Setelah setiap perubahan skema

- Jalankan ulang `GiselX.Web/DatabaseScripts/Reporting_StockAndSellout.sql`. Script ini membuat view
  Power BI (`vw_GiselStock_Live`, `vw_GiselSales_Live`) dan aman dijalankan berulang kali.
- **Jangan** menjalankan salinan lama `Cleanup_DuplicateStockSnapshots.sql`. Versi saat ini sudah
  memperhitungkan area stok; versi lama akan menghapus data area yang sah.

## 5. Pengecekan setelah deploy

1. Buka `/Admin/EmailTest`: pastikan Dashboard Base URL dan folder arsip lolos, lalu kirim email uji.
2. Buka `/hangfire`: job `daily-reminder` ada dan zona waktunya sesuai.
3. Login sebagai user distributor: halaman Stock dan Sales Transactions hanya menampilkan data
   company-nya, termasuk kotak ringkasan.
4. Upload satu file Sellout dan satu file Stock percobaan, cek preview lalu konfirmasi.
