using GiselX.Service.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace GiselX.Service;

public static class ServiceCollectionExtensions
{
    public static void AddGiselXService(this IServiceCollection services)
    {
        // Register services
        services.AddScoped<IServiceLevelService, ServiceLevelService>();
        services.AddScoped<ICompanyService, CompanyService>();
    }
}