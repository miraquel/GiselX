using System.Data;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GiselX.Repository;

public static class ServiceCollectionExtensions
{
    public static void AddGiselXRepository(this IServiceCollection services)
    {
        // register IDbConnection
        services.AddScoped<IDbConnection>(sp =>
        {
            var connectionString = sp.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection");
            return new SqlConnection(connectionString);
        });
        // register IDbTransaction
        services.AddScoped<IDbTransaction>(sp =>
        {
            var dbConnection = sp.GetRequiredService<IDbConnection>();
            if (dbConnection.State != ConnectionState.Open)
            {
                dbConnection.Open();
            }

            return dbConnection.BeginTransaction();
        });
        
        // Register repositories
        services.AddScoped<IServiceLevelRepository, ServiceLevelRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
    }
}