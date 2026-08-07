using GiselX.Service.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace GiselX.Service;

public static class ServiceCollectionExtensions
{
    public static void AddGiselXService(this IServiceCollection services)
    {
        // Register services
        services.AddScoped<IServiceLevelService, ServiceLevelService>();
        services.AddScoped<ISalesTransactionService, SalesTransactionService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<ICompanyService, CompanyService>();
        services.AddScoped<IUploadCheckService, UploadCheckService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ReminderJob>();
    }
}
