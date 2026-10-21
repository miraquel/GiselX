namespace GiselX.Domain;

public enum SelloutUploadGroupStatus
{
    Processing = 0,
    Failed = 1,
    Completed = 2,

    /// <summary>Published its valid rows while excluding some the workbook could not supply cleanly.</summary>
    CompletedWithWarnings = 3
}
