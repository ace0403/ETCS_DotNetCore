using ETCS.Shared.Infrastructure.Email;
using Microsoft.Extensions.DependencyInjection;

namespace ETCS.Shared.Application.Email;

public static class EmailServiceCollectionExtensions
{
    public static IServiceCollection AddEmailNotificationInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IEmailNotificationRepository, EmailNotificationRepository>();
        return services;
    }

    public static IServiceCollection AddGuardianEmailServices(this IServiceCollection services)
    {
        services.AddEmailNotificationInfrastructure();
        services.AddScoped<IOrderEmailContentBuilder, OrderEmailContentBuilder>();
        services.AddScoped<IGuardianEmailNotificationService, GuardianEmailNotificationService>();
        return services;
    }
}
