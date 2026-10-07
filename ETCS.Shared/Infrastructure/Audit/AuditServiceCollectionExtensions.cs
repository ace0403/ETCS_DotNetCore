using Microsoft.Extensions.DependencyInjection;

namespace ETCS.Shared.Infrastructure.Audit;

public static class AuditServiceCollectionExtensions
{
    public static IServiceCollection AddAuditLog(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();
        services.AddScoped<IAuditLogQueryRepository, AuditLogQueryRepository>();
        return services;
    }
}
