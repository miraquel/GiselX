namespace GiselX.Service.Dto;

public sealed class ItemMappingImportErrorDto
{
    public int SourceRowNumber { get; set; }
    public string Field { get; set; } = string.Empty;
    public string? RejectedValue { get; set; }
    public string Reason { get; set; } = string.Empty;
}
