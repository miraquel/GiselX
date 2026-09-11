using System.ComponentModel.DataAnnotations;

namespace GiselX.Service.Dto;

public sealed class SalesmanDto
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    [Display(Name = "Salesman Code")]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(200)]
    [Display(Name = "Salesman Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Status")]
    public bool IsActive { get; set; } = true;
}
