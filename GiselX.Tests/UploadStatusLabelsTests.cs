using GiselX.Domain;
using GiselX.Web;

namespace GiselX.Tests;

public sealed class UploadStatusLabelsTests
{
    [Fact]
    public void Stock_and_sellout_label_each_member_by_meaning_not_by_number()
    {
        // Replaced is 4 for Stock and 3 for Sellout; CompletedWithWarnings is the other way round.
        Assert.Equal("Sudah digantikan upload lain", UploadStatusLabels.For(StockUploadBatchStatus.Replaced));
        Assert.Equal("Sudah digantikan upload lain", UploadStatusLabels.For(SelloutUploadBatchStatus.Replaced));
        Assert.Equal("Berhasil, sebagian baris dilewati",
            UploadStatusLabels.For(StockUploadBatchStatus.CompletedWithWarnings));
        Assert.Equal("Berhasil, sebagian baris dilewati",
            UploadStatusLabels.For(SelloutUploadBatchStatus.CompletedWithWarnings));
    }

    [Fact]
    public void Every_status_has_an_indonesian_label()
    {
        Assert.All(Enum.GetValues<StockUploadBatchStatus>(),
            status => Assert.NotEqual(status.ToString(), UploadStatusLabels.For(status)));
        Assert.All(Enum.GetValues<SelloutUploadBatchStatus>(),
            status => Assert.NotEqual(status.ToString(), UploadStatusLabels.For(status)));
        Assert.All(Enum.GetValues<SelloutUploadGroupStatus>(),
            status => Assert.NotEqual(status.ToString(), UploadStatusLabels.For(status)));
    }
}
