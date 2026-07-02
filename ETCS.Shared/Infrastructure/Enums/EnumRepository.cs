using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Enums;

public sealed class EnumRepository : IEnumRepository
{
    private const string GetActiveTypesSql = """
        SELECT
            et.Id,
            et.EnumTypeName AS Name
        FROM EnumTypes et
        WHERE IsActive = 1
        ORDER BY et.EnumTypeName;
        """;

    private const string GetByTypeIdsSql = """
        SELECT
            e.Id,
            e.EnumTypeId AS TypeId,
            e.EnumValue AS Value,
            e.Description,
            e.SortOrder
        FROM Enums e
        INNER JOIN EnumTypes_N et ON et.Id = e.EnumTypeId
        WHERE e.EnumTypeId IN @TypeIds
        ORDER BY e.EnumTypeId, e.SortOrder, e.EnumValue;
        """;

    private const string GetByIdSql = """
        SELECT TOP (1)
            e.Id,
            e.EnumTypeId AS TypeId,
            e.EnumValue AS Value,
            e.Description,
            e.SortOrder
        FROM Enums e
        INNER JOIN EnumTypes et ON et.Id = e.EnumTypeId
        WHERE e.Id = @Id;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public EnumRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<EnumTypeListItemDto>> GetActiveTypeListAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<EnumTypeListItemDto>(
            new CommandDefinition(
                GetActiveTypesSql,
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<IReadOnlyList<EnumDetailDto>> GetByTypeIdsAsync(
        IReadOnlyCollection<int> typeIds,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<EnumDetailDto>(
            new CommandDefinition(
                GetByTypeIdsSql,
                new { TypeIds = typeIds.ToArray() },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<EnumDetailDto?> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.QueryFirstOrDefaultAsync<EnumDetailDto>(
            new CommandDefinition(
                GetByIdSql,
                new { Id = id },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));
    }
}
