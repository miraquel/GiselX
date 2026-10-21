using System.ComponentModel.DataAnnotations;

namespace GiselX.Service.Dto;

public class CompanyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Business Unit")]
    public int? BusinessUnitId { get; set; }

    public string? BusinessUnitName { get; set; }

    [Display(Name = "Channel")]
    public int? ChannelId { get; set; }

    public string? ChannelName { get; set; }

    [Display(Name = "Salesman")]
    public int? SalesmanId { get; set; }

    public string? SalesmanName { get; set; }

    public string? Address { get; set; }
    public string? ContactEmail { get; set; }
    public int? DeadlineDayOfMonth { get; set; }
    public int? DeadlineDaysOfWeek { get; set; }
    public int ReminderLeadDays { get; set; } = 3;
    public bool? ItemMappingEnabled { get; set; }
}
