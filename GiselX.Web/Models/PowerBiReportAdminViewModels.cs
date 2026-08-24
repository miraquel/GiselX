using System.ComponentModel.DataAnnotations;
using GiselX.Service.Dto;

namespace GiselX.Web.Models;

public sealed class PowerBiReportEditViewModel
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue)]
    public int CompanyId { get; set; }

    [Required, StringLength(150)]
    [Display(Name = "Menu/page name")]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(2048)]
    [Display(Name = "Power BI Publish-to-web URL")]
    public string EmbedUrl { get; set; } = string.Empty;

    [Range(0, int.MaxValue)]
    [Display(Name = "Display order")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IReadOnlyList<CompanyOptionDto> Companies { get; set; } = [];
}

public sealed record PowerBiReportAdminIndexViewModel(
    IReadOnlyList<PowerBiReportDto> Reports,
    IReadOnlyList<CompanyOptionDto> Companies,
    int? CompanyId);

public sealed class PowerBiReportDeleteViewModel
{
    public int Id { get; init; }
    public int? CompanyId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string ReportName { get; init; } = string.Empty;
    public string EmbedUrl { get; init; } = string.Empty;
}
