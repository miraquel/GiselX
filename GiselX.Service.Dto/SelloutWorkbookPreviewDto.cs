namespace GiselX.Service.Dto;

public sealed class SelloutWorkbookPreviewDto
{
    public List<string> WorksheetNames { get; set; } = [];
    public string? WorksheetName { get; set; }
    public int HeaderRowNumber { get; set; }
    public int FirstDataRowNumber { get; set; }
    public List<string> Headers { get; set; } = [];
    public List<string> NormalizedHeaders { get; set; } = [];
    public string HeaderSignature { get; set; } = string.Empty;
    public List<List<string>> SampleRows { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}
