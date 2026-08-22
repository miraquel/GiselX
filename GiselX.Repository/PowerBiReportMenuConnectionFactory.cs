using System.Data.Common;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace GiselX.Repository;

public sealed class PowerBiReportMenuConnectionFactory(IConfiguration configuration)
    : IPowerBiReportMenuConnectionFactory
{
    public DbConnection CreateConnection()
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        return new SqlConnection(connectionString);
    }
}
