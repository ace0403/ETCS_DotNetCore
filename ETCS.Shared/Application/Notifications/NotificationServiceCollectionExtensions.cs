using ETCS.Shared.Infrastructure.Notifications;
using Microsoft.Extensions.DependencyInjection;

namespace ETCS.Shared.Application.Notifications;

public static class NotificationServiceCollectionExtensions
{
    public static IServiceCollection AddGuardianInAppNotificationInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IGuardianNotificationRepository, GuardianNotificationRepository>();
        return services;
    }

    public static IServiceCollection AddGuardianInAppNotificationServices(this IServiceCollection services)
    {
        services.AddGuardianInAppNotificationInfrastructure();
        services.AddScoped<IGuardianInAppNotificationService, GuardianInAppNotificationService>();
        return services;
    }
}
