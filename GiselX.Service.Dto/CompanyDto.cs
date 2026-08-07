namespace GiselX.Service.Dto;

public class CompanyDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ContactEmail { get; set; }
    public int? DeadlineDayOfMonth { get; set; }
    public int? DeadlineDaysOfWeek { get; set; }
    public int ReminderLeadDays { get; set; } = 3;
}
