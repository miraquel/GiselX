using GiselX.Repository.Interface;
using GiselX.Service;

namespace GiselX.Tests;

public sealed class UploadCheckServiceTests
{
    [Fact]
    public async Task Period_is_passed_in_utc_for_the_tables_stamped_in_utc()
    {
        // SalesTransaction and Stock are stamped with SYSUTCDATETIME(). Comparing them against a
        // server-local month moved a WIB upload made before 07:00 on the 1st into the previous month.
        var repository = new RecordingRepository();

        await new UploadCheckService(repository).HasUploadedThisPeriodAsync(1, isWeekly: false);

        Assert.Equal(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1), repository.FromLocal);
        Assert.Equal(repository.FromLocal.ToUniversalTime(), repository.FromUtc);
        Assert.Equal(repository.ToLocal.ToUniversalTime(), repository.ToUtc);
    }

    [Fact]
    public async Task Weekly_period_runs_monday_to_monday_in_both_clocks()
    {
        var repository = new RecordingRepository();

        await new UploadCheckService(repository).HasUploadedThisPeriodAsync(1, isWeekly: true);

        Assert.Equal(DayOfWeek.Monday, repository.FromLocal.DayOfWeek);
        Assert.Equal(TimeSpan.FromDays(7), repository.ToLocal - repository.FromLocal);
        Assert.Equal(repository.FromLocal.ToUniversalTime(), repository.FromUtc);
    }

    private sealed class RecordingRepository : IUploadCheckRepository
    {
        public DateTime FromLocal { get; private set; }
        public DateTime ToLocal { get; private set; }
        public DateTime FromUtc { get; private set; }
        public DateTime ToUtc { get; private set; }

        public Task<bool> HasDataForPeriodAsync(int companyId, DateTime fromLocal, DateTime toLocal,
            DateTime fromUtc, DateTime toUtc)
        {
            (FromLocal, ToLocal, FromUtc, ToUtc) = (fromLocal, toLocal, fromUtc, toUtc);
            return Task.FromResult(true);
        }
    }
}
