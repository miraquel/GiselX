using System.Data;

namespace GiselX.Repository.Interface;

public interface IUploadCheckRepository
{
    /// <summary>
    /// The period is passed twice because the tables disagree on the clock: the staging pipelines stamp
    /// SalesTransaction and Stock with SYSUTCDATETIME(), while the legacy Service Level upload stamps
    /// TransDist with server-local DateTime.Now.
    /// </summary>
    Task<bool> HasDataForPeriodAsync(int companyId, DateTime fromLocal, DateTime toLocal,
        DateTime fromUtc, DateTime toUtc);
}
