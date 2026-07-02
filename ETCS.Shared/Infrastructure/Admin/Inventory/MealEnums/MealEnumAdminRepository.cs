using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;

public sealed class MealEnumAdminRepository : IMealEnumAdminRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public MealEnumAdminRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<MealEnumLookupDto>> GetByTypeIdAsync(int enumTypeId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<MealEnumLookupDto>(
            new CommandDefinition(
                """
                SELECT
                    Id,
                    EnumValue AS Name,
                    ISNULL(Description, EnumValue) AS Description,
                    ISNULL(SortOrder, 0) AS SortOrder
                FROM Enums
                WHERE EnumTypeId = @EnumTypeId AND ISNULL(IsActive, 1) = 1
                ORDER BY SortOrder, EnumValue;
                """,
                new { EnumTypeId = enumTypeId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }
}
