using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ETCS.Shared.Infrastructure.HealthChecks;

public sealed class SqlMealDatabaseHealthCheck : IHealthCheck
{
    private readonly IMealDbConnectionFactory _mealDbConnectionFactory;

    public SqlMealDatabaseHealthCheck(IMealDbConnectionFactory mealDbConnectionFactory)
    {
        _mealDbConnectionFactory = mealDbConnectionFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _mealDbConnectionFactory.CreateConnection();
            var dbConnection = (DbConnection)connection;
            await dbConnection.OpenAsync(cancellationToken);
            var result = await dbConnection.QuerySingleAsync<int>(
                new CommandDefinition("SELECT 1", cancellationToken: cancellationToken));
            return result == 1
                ? HealthCheckResult.Healthy("Meal SQL Server is reachable.")
                : HealthCheckResult.Unhealthy("Meal SQL Server ping returned unexpected value.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Meal SQL Server health check failed.", ex);
        }
    }
}
