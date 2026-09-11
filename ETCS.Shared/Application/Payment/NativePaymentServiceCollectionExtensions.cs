using Microsoft.Extensions.DependencyInjection;

namespace ETCS.Shared.Application.Payment;

public static class NativePaymentServiceCollectionExtensions
{
    public static IServiceCollection AddNativePaymentFlowServices(this IServiceCollection services)
    {
        services.AddScoped<INativeTopupInitiateService, NativeTopupInitiateService>();
        services.AddScoped<INativeOrderInitiateService, NativeOrderInitiateService>();
        services.AddScoped<INativeWalletRegistrationService, NativeWalletRegistrationService>();
        return services;
    }
}
