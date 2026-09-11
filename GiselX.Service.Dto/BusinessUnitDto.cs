using System.ComponentModel.DataAnnotations;

namespace GiselX.Service.Dto;

public sealed class BusinessUnitDto
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Status")]
    public bool IsActive { get; set; } = true;
}
