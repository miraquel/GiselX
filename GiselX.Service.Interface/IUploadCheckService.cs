namespace GiselX.Service.Interface;

public interface IUploadCheckService
{
    Task<bool> HasUploadedThisPeriodAsync(int companyId, bool isWeekly);
}
