namespace GiselX.Service;

public sealed class SelloutUploadOptions
{
    public const string SectionName = "SelloutUpload";
    public long MaxFileSizeBytes { get; set; } = 20 * 1024 * 1024;
}
