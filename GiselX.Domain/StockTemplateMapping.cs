namespace GiselX.Domain;

public sealed class StockTemplateMapping
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorksheetName { get; set; } = string.Empty;
    public int HeaderRowNumber { get; set; }
    public int FirstDataRowNumber { get; set; }
    public string HeaderSignature { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string? GoodConditionValues { get; set; }
    public string? BadConditionValues { get; set; }
    public DateTime CreatedDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? ModifiedDate { get; set; }
    public string? ModifiedBy { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<StockTemplateColumn> Columns { get; set; } = new List<StockTemplateColumn>();
    public ICollection<StockUploadBatch> UploadBatches { get; set; } = new List<StockUploadBatch>();
}
