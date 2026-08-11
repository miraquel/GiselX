namespace GiselX.Service.Dto;

public sealed class SaveItemMappingDto
{
    public int? Id { get; set; }
    public int CompanyId { get; set; }
    public string GmkItemId { get; set; } = string.Empty;
    public string GmkItemName { get; set; } = string.Empty;
    public string DistributorItemId { get; set; } = string.Empty;
    public string DistributorItemName { get; set; } = string.Empty;
}
