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
                    ISNULL(SortOrder, 0) AS SortOrder,
                    ParentId
                FROM Enums
                WHERE EnumTypeId = @EnumTypeId AND ISNULL(IsActive, 1) = 1
                ORDER BY SortOrder, EnumValue;
                """,
                new { EnumTypeId = enumTypeId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MealEnumLookupDto>> GetMealSessionsAsync(CancellationToken cancellationToken = default)
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
                    ISNULL(SortOrder, 0) AS SortOrder,
                    ParentId
                FROM Enums
                WHERE EnumTypeId = @EnumTypeId
                  AND ParentId IS NULL
                  AND ISNULL(IsActive, 1) = 1
                ORDER BY SortOrder, EnumValue;
                """,
                new { EnumTypeId = MealEnumTypeIds.MealType },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MealEnumLookupDto>> GetMealTypesBySessionAsync(
        int sessionId,
        CancellationToken cancellationToken = default)
    {
        if (sessionId <= 0)
        {
            return [];
        }

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
                    ISNULL(SortOrder, 0) AS SortOrder,
                    ParentId
                FROM Enums
                WHERE EnumTypeId = @EnumTypeId
                  AND ParentId = @SessionId
                  AND ISNULL(IsActive, 1) = 1
                ORDER BY SortOrder, EnumValue;
                """,
                new { EnumTypeId = MealEnumTypeIds.MealType, SessionId = sessionId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<bool> IsMealTypeInSessionAsync(
        int mealTypeId,
        int mealSessionId,
        CancellationToken cancellationToken = default)
    {
        if (mealTypeId <= 0 || mealSessionId <= 0)
        {
            return false;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await dbConnection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM Enums
                    WHERE Id = @MealTypeId
                      AND EnumTypeId = @EnumTypeId
                      AND ParentId = @MealSessionId
                      AND ISNULL(IsActive, 1) = 1
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;
                """,
                new
                {
                    MealTypeId = mealTypeId,
                    MealSessionId = mealSessionId,
                    EnumTypeId = MealEnumTypeIds.MealType
                },
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MealEnumLookupDto>> GetStudentOrderTypesAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var ids = StudentOrderTypeOptionIds.Ordered;
        var rows = await dbConnection.QueryAsync<MealEnumLookupDto>(
            new CommandDefinition(
                """
                SELECT
                    Id,
                    EnumValue AS Name,
                    ISNULL(Description, EnumValue) AS Description,
                    ISNULL(SortOrder, 0) AS SortOrder
                FROM Enums
                WHERE ISNULL(IsActive, 1) = 1
                  AND (
                        EnumTypeId = @EnumTypeId
                        OR Id IN @Ids
                      );
                """,
                new
                {
                    EnumTypeId = MealEnumTypeIds.StudentTransactionType,
                    Ids = ids.ToArray()
                },
                cancellationToken: cancellationToken));

        var byId = rows
            .GroupBy(x => x.Id)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<MealEnumLookupDto>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            var id = ids[i];
            if (byId.TryGetValue(id, out var row) && !string.IsNullOrWhiteSpace(row.Name))
            {
                result.Add(row);
                continue;
            }

            result.Add(new MealEnumLookupDto
            {
                Id = id,
                Name = StudentOrderTypeOptionIds.DisplayName(id),
                Description = StudentOrderTypeOptionIds.DisplayName(id),
                SortOrder = i + 1
            });
        }

        return result;
    }
}
