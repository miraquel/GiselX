using GiselX.Service.Dto;

namespace GiselX.Web.Models;

public sealed class MappingCopyViewModel
{
    public int TargetCompanyId { get; set; }
    public string TargetCompanyName { get; set; } = string.Empty;
    public int? SourceCompanyId { get; set; }
    public IReadOnlyList<CopySourceCompanyDto> Sources { get; set; } = [];
    public MappingCopyPreviewDto? Preview { get; set; }
    public bool CanCopySellout { get; set; }
    public bool CanCopyStock { get; set; }
    public bool CanCopyItems { get; set; }
}

public sealed class MappingCopyResultViewModel
{
    public int TargetCompanyId { get; set; }
    public string TargetCompanyName { get; set; } = string.Empty;
    public string? Message { get; set; }
    public MappingCopyResultDto Result { get; set; } = new();
}
