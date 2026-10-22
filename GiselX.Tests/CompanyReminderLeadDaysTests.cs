using GiselX.Domain;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Tests;

/// <summary>
/// The lead reminder is sent ReminderLeadDays before the deadline. A weekly deadline seven or more days
/// out would be reminded about before the previous deadline had even passed, and nothing stopped an admin
/// typing 400.
/// </summary>
public sealed class CompanyReminderLeadDaysTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(29)]
    public async Task A_monthly_lead_outside_0_to_28_days_is_refused(int leadDays)
    {
        var (service, repository) = Service();

        var response = await service.CreateCompanyAsync(new CompanyDto
            { Name = "A", DeadlineDayOfMonth = 5, ReminderLeadDays = leadDays }, default);

        Assert.False(response.IsSuccess);
        Assert.Contains("Reminder lead days must be between 0 and 28 for a monthly deadline.", response.Errors);
        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task A_weekly_lead_of_seven_days_or_more_is_refused()
    {
        var (service, repository) = Service();

        var response = await service.UpdateCompanyAsync(new CompanyDto
            { Id = 7, Name = "A", DeadlineDaysOfWeek = (int)WeekDays.Monday, ReminderLeadDays = 7 }, default);

        Assert.False(response.IsSuccess);
        Assert.Contains("Reminder lead days must be between 0 and 6 for a weekly deadline.", response.Errors);
        Assert.Empty(repository.Saved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(28)]
    public async Task A_monthly_lead_inside_the_range_is_saved(int leadDays)
    {
        var (service, repository) = Service();

        await service.CreateCompanyAsync(new CompanyDto
            { Name = "A", DeadlineDayOfMonth = 5, ReminderLeadDays = leadDays }, default);

        Assert.Equal(leadDays, Assert.Single(repository.Saved).ReminderLeadDays);
    }

    private static (CompanyService, CompanyServiceFakes.RecordingCompanyRepository) Service()
    {
        var repository = new CompanyServiceFakes.RecordingCompanyRepository();
        return (new CompanyService(repository, new CompanyServiceFakes.NoItemMappings(),
            new CompanyServiceFakes.RecordingTransaction(), new UserClaimDto()), repository);
    }
}
