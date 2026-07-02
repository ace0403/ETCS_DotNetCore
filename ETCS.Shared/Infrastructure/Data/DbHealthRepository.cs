using System.Data;
using Dapper;

namespace ETCS.Shared.Infrastructure.Data;

public sealed class DbHealthRepository : IDbHealthRepository
{
    private readonly IDbConnectionFactory _dbConnectionFactory;

    public DbHealthRepository(IDbConnectionFactory dbConnectionFactory)
    {
        _dbConnectionFactory = dbConnectionFactory;
    }

    public async Task<int> PingAsync(CancellationToken cancellationToken)
    {
        using var connection = _dbConnectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<int>(new CommandDefinition("SELECT 1", cancellationToken: cancellationToken));
    }
}
