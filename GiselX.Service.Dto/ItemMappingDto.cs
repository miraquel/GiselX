namespace GiselX.Service.Dto;

public sealed class ItemMappingDto
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string GmkItemId { get; set; } = string.Empty;
    public string GmkItemName { get; set; } = string.Empty;
    public string DistributorItemId { get; set; } = string.Empty;
    public string DistributorItemName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }
}
