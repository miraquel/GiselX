namespace GiselX.Domain;

public enum StockUploadBatchStatus
{
    Processing = 0,
    Failed = 1,
    Completed = 2,
    CompletedWithWarnings = 3
}
