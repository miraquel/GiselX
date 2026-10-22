using GiselX.Domain;

namespace GiselX.Web;

/// <summary>
/// The words a distributor reads for an upload's status, instead of the enum name
/// ("CompletedWithWarnings"). One overload per enum because the enums number their members
/// differently; matching on the member, never on the number, keeps the labels right for both.
/// </summary>
public static class UploadStatusLabels
{
    public static string For(StockUploadBatchStatus status) => status switch
    {
        StockUploadBatchStatus.Processing => "Diproses",
        StockUploadBatchStatus.Failed => "Gagal",
        StockUploadBatchStatus.Completed => "Berhasil",
        StockUploadBatchStatus.CompletedWithWarnings => "Berhasil, sebagian baris dilewati",
        StockUploadBatchStatus.Replaced => "Sudah digantikan upload lain",
        _ => status.ToString()
    };

    public static string For(SelloutUploadBatchStatus status) => status switch
    {
        SelloutUploadBatchStatus.Processing => "Diproses",
        SelloutUploadBatchStatus.Failed => "Gagal",
        SelloutUploadBatchStatus.Completed => "Berhasil",
        SelloutUploadBatchStatus.CompletedWithWarnings => "Berhasil, sebagian baris dilewati",
        SelloutUploadBatchStatus.Replaced => "Sudah digantikan upload lain",
        _ => status.ToString()
    };

    public static string For(SelloutUploadGroupStatus status) => status switch
    {
        SelloutUploadGroupStatus.Processing => "Diproses",
        SelloutUploadGroupStatus.Failed => "Gagal",
        SelloutUploadGroupStatus.Completed => "Berhasil",
        SelloutUploadGroupStatus.CompletedWithWarnings => "Berhasil, sebagian baris dilewati",
        _ => status.ToString()
    };

    /// <summary>Upload times are stored in UTC; a distributor reads them in the server's local time.</summary>
    public static string LocalTime(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime().ToString("dd MMM yyyy HH:mm");
}
