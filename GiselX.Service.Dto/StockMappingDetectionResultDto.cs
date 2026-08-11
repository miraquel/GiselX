namespace GiselX.Service.Dto;

public sealed class StockMappingDetectionResultDto
{
    public StockTemplateMappingDto? Mapping { get; set; }
    public List<string> CandidateMappingNames { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}
