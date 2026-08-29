using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Infrastructure.Email;
using ETCS.Shared.Infrastructure.Transaction;
using Microsoft.Extensions.DependencyInjection;

namespace ETCS.Shared.Application.Email;

public static class EmailDeliveryWorkerServiceCollectionExtensions
{
    /// <summary>
    /// Registers MealDB email infrastructure and the delivery BackgroundService.
    /// Host this only in ETCS.EmailWorker (not in ETCS.API) so API deploys do not stop sending.
    /// </summary>
    public static IServiceCollection AddEmailDeliveryWorker(this IServiceCollection services)
    {
        services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
        services.AddSingleton<IMealDbConnectionFactory, SqlMealConnectionFactory>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddEmailNotificationInfrastructure();
        services.AddHostedService<EmailDeliveryBackgroundService>();
        return services;
    }
}
