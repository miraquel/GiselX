using GiselX.Domain;

namespace GiselX.Service.Dto;

public sealed class StockStagingErrorDto
{
    public int SourceRowNumber { get; set; }
    public StockCanonicalField? Field { get; set; }
    public string? RejectedValue { get; set; }
    public string Reason { get; set; } = string.Empty;
}
