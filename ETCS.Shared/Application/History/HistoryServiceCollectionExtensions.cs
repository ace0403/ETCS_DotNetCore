using ETCS.Shared.Application.Orders;
using ETCS.Shared.Infrastructure.History;
using Microsoft.Extensions.DependencyInjection;

namespace ETCS.Shared.Application.History;

public static class HistoryServiceCollectionExtensions
{
    public static IServiceCollection AddGuardianHistoryServices(this IServiceCollection services)
    {
        services.AddScoped<IHistoryDetailRepository, HistoryDetailRepository>();
        services.AddScoped<OrderPaymentSummaryBuilder>();
        services.AddScoped<IGuardianHistoryService, GuardianHistoryService>();
        return services;
    }
}
