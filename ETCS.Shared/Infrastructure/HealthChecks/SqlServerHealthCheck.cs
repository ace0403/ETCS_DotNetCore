using ETCS.Shared.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ETCS.Shared.Infrastructure.HealthChecks;

public sealed class SqlServerHealthCheck : IHealthCheck
{
    private readonly IDbHealthRepository _dbHealthRepository;

    public SqlServerHealthCheck(IDbHealthRepository dbHealthRepository)
    {
        _dbHealthRepository = dbHealthRepository;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _dbHealthRepository.PingAsync(cancellationToken);
            return result == 1
                ? HealthCheckResult.Healthy("SQL Server is reachable.")
                : HealthCheckResult.Unhealthy("SQL Server ping returned unexpected value.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server health check failed.", ex);
        }
    }
}
