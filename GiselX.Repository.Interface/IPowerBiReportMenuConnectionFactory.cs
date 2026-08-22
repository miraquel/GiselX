using System.Data.Common;

namespace GiselX.Repository.Interface;

public interface IPowerBiReportMenuConnectionFactory
{
    DbConnection CreateConnection();
}
