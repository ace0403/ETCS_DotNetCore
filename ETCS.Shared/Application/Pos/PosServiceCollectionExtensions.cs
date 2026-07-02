using ETCS.Shared.Infrastructure.Pos;
using Microsoft.Extensions.DependencyInjection;

namespace ETCS.Shared.Application.Pos;

public static class PosServiceCollectionExtensions
{
    public static IServiceCollection AddPosServices(this IServiceCollection services)
    {
        services.AddScoped<IPosTerminalRepository, PosTerminalRepository>();
        services.AddScoped<IPosCatalogRepository, PosCatalogRepository>();
        services.AddScoped<IPOSOrderRepository, POSOrderRepository>();
        services.AddScoped<IPosSpendRepository, PosSpendRepository>();
        services.AddScoped<IPosLegacyTransactionRepository, PosLegacyTransactionRepository>();
        services.AddScoped<IPosOrderInitiateService, PosOrderInitiateService>();
        services.AddScoped<IPosOrderCompleteService, PosOrderCompleteService>();
        return services;
    }
}
