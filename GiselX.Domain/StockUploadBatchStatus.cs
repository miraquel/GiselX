namespace GiselX.Domain;

public enum StockUploadBatchStatus
{
    Processing = 0,
    Failed = 1,
    Completed = 2,
    CompletedWithWarnings = 3,

    /// <summary>
    /// A later upload for the same company and snapshot date took its place, and its rows were
    /// removed from the Stock table.
    ///
    /// Numbered 4, which is NOT what it means in <see cref="SelloutUploadBatchStatus"/> — there
    /// Replaced is 3 and CompletedWithWarnings is 4. The two enums cannot be aligned without
    /// rewriting status values already stored against thousands of batches, so any query touching
    /// both must use each enum's own numbering rather than assuming they match.
    /// </summary>
    Replaced = 4
}
