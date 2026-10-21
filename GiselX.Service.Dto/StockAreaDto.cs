using System.ComponentModel.DataAnnotations;

namespace GiselX.Service.Dto;

public sealed class StockAreaDto
{
    public int Id { get; set; }

    public int CompanyId { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Area Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Aliases { get; set; }

    [Display(Name = "Status")]
    public bool IsActive { get; set; } = true;
}
