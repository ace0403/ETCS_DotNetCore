using ETCS.Shared.Application.Payment;
using ETCS.Shared.Application.Students;
using Microsoft.Extensions.DependencyInjection;

namespace ETCS.Shared.Application.Orders;

public static class OrderServiceCollectionExtensions
{
    public static IServiceCollection AddOrderFlowServices(this IServiceCollection services)
    {
        services.AddScoped<IStudentOrderTypeAccessService, StudentOrderTypeAccessService>();
        services.AddScoped<IOrderInitiateService, OrderInitiateService>();
        services.AddScoped<IOrderPaymentCompleteService, OrderPaymentCompleteService>();
        services.AddSingleton<PaymentCompletionCancellation>();
        return services;
    }
}
