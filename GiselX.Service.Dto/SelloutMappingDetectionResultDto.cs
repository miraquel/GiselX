namespace GiselX.Service.Dto;

public sealed class SelloutMappingDetectionResultDto
{
    public bool IsMatch => Mapping is not null && Errors.Count == 0;
    public DistributorTemplateMappingDto? Mapping { get; set; }
    public List<string> CandidateMappingNames { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}
