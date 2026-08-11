namespace GiselX.Service.Dto;

public sealed class ItemMappingImportResultDto
{
    public int NewRows { get; set; }
    public int UpdatedRows { get; set; }
    public int UnchangedRows { get; set; }
    public int InvalidRows { get; set; }
    public IReadOnlyList<ItemMappingImportErrorDto> Errors { get; set; } = [];
}
