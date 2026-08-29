using Microsoft.Extensions.DependencyInjection;

namespace ETCS.Shared.Application.Topup;

public static class TopupServiceCollectionExtensions
{
    public static IServiceCollection AddTopupFlowServices(this IServiceCollection services)
    {
        services.AddScoped<ITopupInitiateService, TopupInitiateService>();
        services.AddScoped<ITopupPaymentCompleteService, TopupPaymentCompleteService>();
        services.AddScoped<IManualTopupService, ManualTopupService>();
        return services;
    }
}
