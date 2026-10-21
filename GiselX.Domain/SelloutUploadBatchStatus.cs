namespace GiselX.Domain;

public enum SelloutUploadBatchStatus
{
    Processing = 0,
    Failed = 1,
    Completed = 2,
    Replaced = 3,

    /// <summary>Published its valid rows while excluding some the workbook could not supply cleanly.</summary>
    CompletedWithWarnings = 4
}
