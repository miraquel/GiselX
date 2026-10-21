using System.ComponentModel.DataAnnotations;

namespace GiselX.Service.Dto;

public sealed class BusinessUnitDto
{
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Internal Notification Emails")]
    public string? NotificationEmails { get; set; }

    [Display(Name = "Status")]
    public bool IsActive { get; set; } = true;
}
