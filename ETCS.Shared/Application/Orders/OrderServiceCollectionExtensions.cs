using ETCS.Shared.Application.Payment;
using Microsoft.Extensions.DependencyInjection;

namespace ETCS.Shared.Application.Orders;

public static class OrderServiceCollectionExtensions
{
    public static IServiceCollection AddOrderFlowServices(this IServiceCollection services)
    {
        services.AddScoped<IOrderInitiateService, OrderInitiateService>();
        services.AddScoped<IOrderPaymentCompleteService, OrderPaymentCompleteService>();
        services.AddSingleton<PaymentCompletionCancellation>();
        return services;
    }
}
