namespace GiselX.Service.Interface;

public interface IEmailService
{
    Task SendReminderAsync(string toEmail, string companyName, DateTime deadline, bool isFinalReminder);
}
