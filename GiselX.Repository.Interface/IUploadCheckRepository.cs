using System.Data;

namespace GiselX.Repository.Interface;

public interface IUploadCheckRepository
{
    Task<bool> HasDataForPeriodAsync(int companyId, DateTime from, DateTime to);
}
