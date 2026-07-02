using System.Data;
using ETCS.Shared.Options;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace ETCS.Shared.Infrastructure.Data;

public sealed class SqlMealConnectionFactory : IMealDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlMealConnectionFactory(IOptions<MealDatabaseOptions> mealDatabaseOptions)
    {
        _connectionString = mealDatabaseOptions.Value.ConnectionString;
    }

    public IDbConnection CreateConnection()
    {
        return new SqlConnection(_connectionString);
    }
}
