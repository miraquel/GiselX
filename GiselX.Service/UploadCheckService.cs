using GiselX.Repository.Interface;
using GiselX.Service.Interface;

namespace GiselX.Service;

public class UploadCheckService : IUploadCheckService
{
    private readonly IUploadCheckRepository _repository;

    public UploadCheckService(IUploadCheckRepository repository)
    {
        _repository = repository;
    }

    public Task<bool> HasUploadedThisPeriodAsync(int companyId, bool isWeekly)
    {
        var (from, to) = isWeekly ? GetCurrentIsoWeek() : GetCurrentMonth();
        return _repository.HasDataForPeriodAsync(companyId, from, to);
    }

    private static (DateTime From, DateTime To) GetCurrentMonth()
    {
        var today = DateTime.Today;
        var from = new DateTime(today.Year, today.Month, 1);
        return (from, from.AddMonths(1));
    }

    private static (DateTime From, DateTime To) GetCurrentIsoWeek()
    {
        var today = DateTime.Today;
        var dayOfWeek = (int)today.DayOfWeek;           // Sun=0, Mon=1...Sat=6
        var daysFromMonday = dayOfWeek == 0 ? 6 : dayOfWeek - 1;
        var from = today.AddDays(-daysFromMonday);       // most recent Monday 00:00
        return (from, from.AddDays(7));                  // next Monday 00:00
    }
}
