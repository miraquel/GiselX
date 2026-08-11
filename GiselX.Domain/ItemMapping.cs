namespace GiselX.Domain;

public sealed class ItemMapping
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string GmkItemId { get; set; } = string.Empty;
    public string GmkItemName { get; set; } = string.Empty;
    public string DistributorItemId { get; set; } = string.Empty;
    public string DistributorItemName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<Stock> Stocks { get; set; } = [];
    public ICollection<SalesTransaction> SalesTransactions { get; set; } = [];
}
