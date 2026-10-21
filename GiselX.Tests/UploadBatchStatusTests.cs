using GiselX.Domain;

namespace GiselX.Tests;

/// <summary>
/// The two upload pipelines number their statuses differently, and the difference is load-bearing:
/// every hand-written query that filters "live" batches — the reporting views among them — encodes
/// these numbers. Aligning the enums would look tidy and would silently rewrite the meaning of rows
/// already stored against every batch ever published, so the divergence is pinned here rather than
/// left to be discovered by a report that quietly returns the wrong rows.
/// </summary>
public sealed class UploadBatchStatusTests
{
    [Fact]
    public void Stock_numbers_replaced_after_completed_with_warnings()
    {
        Assert.Equal(2, (int)StockUploadBatchStatus.Completed);
        Assert.Equal(3, (int)StockUploadBatchStatus.CompletedWithWarnings);
        Assert.Equal(4, (int)StockUploadBatchStatus.Replaced);
    }

    [Fact]
    public void Sellout_numbers_them_the_other_way_round()
    {
        Assert.Equal(2, (int)SelloutUploadBatchStatus.Completed);
        Assert.Equal(3, (int)SelloutUploadBatchStatus.Replaced);
        Assert.Equal(4, (int)SelloutUploadBatchStatus.CompletedWithWarnings);
    }

    [Fact]
    public void The_two_enums_disagree_and_must_not_be_aligned()
    {
        Assert.NotEqual((int)StockUploadBatchStatus.Replaced, (int)SelloutUploadBatchStatus.Replaced);
        Assert.NotEqual((int)StockUploadBatchStatus.CompletedWithWarnings,
            (int)SelloutUploadBatchStatus.CompletedWithWarnings);
    }
}
