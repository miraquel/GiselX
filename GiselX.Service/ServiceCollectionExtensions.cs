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
        services.AddScoped<IDistributorTemplateMappingService, DistributorTemplateMappingService>();
        services.AddScoped<IStockTemplateMappingService, StockTemplateMappingService>();
        services.AddScoped<ISelloutWorkbookInspector, XlsxSelloutWorkbookInspector>();
        services.AddScoped<IStockWorkbookInspector, XlsxStockWorkbookInspector>();
        services.AddScoped<StockWorkbookParser>();
        services.AddScoped<IStockWorkbookAnalysisService, StockWorkbookAnalysisService>();
        services.AddScoped<IStockUploadService, StockUploadService>();
        services.AddScoped<SelloutWorkbookParser>();
        services.AddScoped<ISelloutWorkbookAnalysisService, SelloutWorkbookAnalysisService>();
        services.AddScoped<ISelloutUploadService, SelloutUploadService>();
        services.AddScoped<IItemMappingService, ItemMappingService>();
        services.AddScoped<ItemMappingWorkbookParser>();
        services.AddScoped<IItemMappingImportService, ItemMappingImportService>();
        services.AddScoped<IItemMappingResolver, ItemMappingResolver>();
    }
}
