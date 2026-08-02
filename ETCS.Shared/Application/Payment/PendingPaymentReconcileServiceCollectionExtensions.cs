using ETCS.Shared.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ETCS.Shared.Application.Payment;

public static class PendingPaymentReconcileServiceCollectionExtensions
{
    public static IServiceCollection AddPendingPaymentReconcileServices(this IServiceCollection services)
    {
        services.AddScoped<IPendingPaymentReconcileService, PendingPaymentReconcileService>();
        return services;
    }
}
