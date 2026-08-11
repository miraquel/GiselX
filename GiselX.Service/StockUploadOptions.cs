namespace GiselX.Service;

public sealed class StockUploadOptions
{
    public const string SectionName = "StockUpload";
    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;
}
