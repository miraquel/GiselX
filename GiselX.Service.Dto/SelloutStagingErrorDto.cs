using GiselX.Domain;

namespace GiselX.Service.Dto;

public sealed class SelloutStagingErrorDto
{
    public int SourceRowNumber { get; set; }
    public SelloutCanonicalField? Field { get; set; }
    public string? RejectedValue { get; set; }
    public string Reason { get; set; } = string.Empty;
}
