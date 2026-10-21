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
        services.AddScoped<ISalesTransactionRepository, SalesTransactionRepository>();
        services.AddScoped<IStockRepository, StockRepository>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<IBusinessUnitRepository, BusinessUnitRepository>();
        services.AddScoped<IStockAreaRepository, StockAreaRepository>();
        services.AddScoped<IPowerBiReportMenuConnectionFactory, PowerBiReportMenuConnectionFactory>();
        services.AddScoped<IPowerBiReportMenuRepository, PowerBiReportMenuRepository>();
        services.AddScoped<IPowerBiReportRepository, PowerBiReportRepository>();
        services.AddScoped<IUploadCheckRepository, UploadCheckRepository>();
        services.AddScoped<IStandaloneConnectionFactory, StandaloneConnectionFactory>();
        services.AddScoped<IUploadNotificationRepository, UploadNotificationRepository>();
        services.AddScoped<IEmailSettingRepository, EmailSettingRepository>();
        services.AddScoped<ILatestUploadRepository, LatestUploadRepository>();
        services.AddScoped<IDistributorTemplateMappingRepository, DistributorTemplateMappingRepository>();
        services.AddScoped<IStockTemplateMappingRepository, StockTemplateMappingRepository>();
        services.AddScoped<ISelloutUploadRepository, SelloutUploadRepository>();
        services.AddScoped<IStockUploadRepository, StockUploadRepository>();
        services.AddScoped<IItemMappingRepository, ItemMappingRepository>();
        services.AddScoped<IChannelRepository, ChannelRepository>();
        services.AddScoped<ISalesmanRepository, SalesmanRepository>();
    }
}
