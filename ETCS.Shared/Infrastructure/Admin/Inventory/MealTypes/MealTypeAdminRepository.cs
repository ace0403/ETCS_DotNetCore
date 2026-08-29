using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealTypes;

public sealed class MealTypeAdminRepository : IMealTypeAdminRepository
{
    private const int MealTypeEnumTypeId = MealEnumTypeIds.MealType;

    private readonly IMealDbConnectionFactory _connectionFactory;

    public MealTypeAdminRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SessionSelectSql = """
        SELECT Id,
               EnumValue AS Name,
               ISNULL(SortOrder, 0) AS SortOrder,
               ISNULL(IsActive, 1) AS IsActive
        """;

    private const string SessionFromSql = "FROM Enums";
    private const string SessionBaseFilterSql = "EnumTypeId = @EnumTypeId AND ParentId IS NULL";
    private const string SessionSearchFilterSql = "LTRIM(RTRIM(ISNULL(EnumValue, ''))) LIKE '%' + @Search + '%'";

    private static readonly IReadOnlyDictionary<string, string> SessionSortColumns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "Id",
            ["Name"] = "EnumValue",
            ["SortOrder"] = "SortOrder",
            ["IsActive"] = "IsActive"
        };

    private const string TypeSelectSql = """
        SELECT t.Id,
               t.EnumValue AS Name,
               t.ParentId AS SessionId,
               ISNULL(s.EnumValue, '') AS SessionName,
               ISNULL(t.SortOrder, 0) AS SortOrder,
               ISNULL(t.IsActive, 1) AS IsActive
        """;

    private const string TypeFromSql = """
        FROM Enums t
        INNER JOIN Enums s ON s.Id = t.ParentId AND s.EnumTypeId = @EnumTypeId
        """;

    private const string TypeSearchFilterSql = """
        (
            LTRIM(RTRIM(ISNULL(t.EnumValue, ''))) LIKE '%' + @Search + '%'
            OR LTRIM(RTRIM(ISNULL(s.EnumValue, ''))) LIKE '%' + @Search + '%'
        )
        """;

    private static readonly IReadOnlyDictionary<string, string> TypeSortColumns =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "t.Id",
            ["Name"] = "t.EnumValue",
            ["SessionId"] = "t.ParentId",
            ["SessionName"] = "s.EnumValue",
            ["SortOrder"] = "t.SortOrder",
            ["IsActive"] = "t.IsActive"
        };

    public async Task<DataTableResponse<MealSessionListItemDto>> GetSessionDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await QueryPagedAsync<MealSessionListItemDto>(
            dbConnection,
            SessionSelectSql,
            SessionFromSql,
            SessionBaseFilterSql,
            SessionSearchFilterSql,
            SessionSortColumns,
            "SortOrder, EnumValue",
            request,
            new { EnumTypeId = MealTypeEnumTypeId },
            cancellationToken);
    }

    public async Task<DataTableResponse<MealTypeListItemDto>> GetTypeDataAsync(
        DataTableRequest request,
        int? sessionId,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var baseFilter = "t.EnumTypeId = @EnumTypeId AND t.ParentId IS NOT NULL";
        object parameters;
        if (sessionId is > 0)
        {
            baseFilter += " AND t.ParentId = @SessionId";
            parameters = new { EnumTypeId = MealTypeEnumTypeId, SessionId = sessionId.Value };
        }
        else
        {
            parameters = new { EnumTypeId = MealTypeEnumTypeId };
        }

        return await QueryPagedAsync<MealTypeListItemDto>(
            dbConnection,
            TypeSelectSql,
            TypeFromSql,
            baseFilter,
            TypeSearchFilterSql,
            TypeSortColumns,
            "t.SortOrder, s.EnumValue, t.EnumValue",
            request,
            parameters,
            cancellationToken);
    }

    public async Task<MealTypeSaveRequest?> GetAsync(
        int id,
        string kind,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        var parentFilter = MealTypeKinds.IsType(kind)
            ? "ParentId IS NOT NULL"
            : "ParentId IS NULL";

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await dbConnection.QuerySingleOrDefaultAsync<MealTypeSaveRequest>(
            new CommandDefinition(
                $"""
                SELECT Id,
                       EnumValue AS Name,
                       ParentId,
                       ISNULL(SortOrder, 0) AS SortOrder,
                       ISNULL(IsActive, 1) AS IsActive,
                       @Kind AS Kind
                FROM Enums
                WHERE Id = @Id
                  AND EnumTypeId = @EnumTypeId
                  AND {parentFilter};
                """,
                new
                {
                    Id = id,
                    EnumTypeId = MealTypeEnumTypeId,
                    Kind = MealTypeKinds.IsType(kind) ? MealTypeKinds.Type : MealTypeKinds.Session
                },
                cancellationToken: cancellationToken));
    }

    public async Task<AdminOperationResult> SaveAsync(
        MealTypeSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return AdminOperationResult.Fail("Name is required.");
        }

        var isType = MealTypeKinds.IsType(request.Kind);
        if (!isType && !MealTypeKinds.IsSession(request.Kind))
        {
            return AdminOperationResult.Fail("A valid kind is required.");
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        int? parentId = null;
        if (isType)
        {
            if (request.ParentId is not > 0)
            {
                return AdminOperationResult.Fail("Meal session is required.");
            }

            parentId = request.ParentId.Value;
            if (!await SessionExistsAsync(dbConnection, parentId.Value, cancellationToken))
            {
                return AdminOperationResult.Fail("Selected meal session was not found.");
            }
        }

        if (await NameExistsAsync(dbConnection, name, parentId, request.Id, cancellationToken))
        {
            return isType
                ? AdminOperationResult.Fail("A meal type with this name already exists in the selected session.")
                : AdminOperationResult.Fail("A meal session with this name already exists.");
        }

        if (request.Id > 0)
        {
            return await UpdateAsync(dbConnection, request, name, parentId, isType, cancellationToken);
        }

        return await InsertAsync(dbConnection, request, name, parentId, isType, cancellationToken);
    }

    public async Task<AdminOperationResult> DeleteAsync(
        int id,
        string kind,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return AdminOperationResult.Fail("Id is required.");
        }

        var isType = MealTypeKinds.IsType(kind);
        if (!isType && !MealTypeKinds.IsSession(kind))
        {
            return AdminOperationResult.Fail("A valid kind is required.");
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        if (!await RowExistsAsync(dbConnection, id, isType, cancellationToken))
        {
            return AdminOperationResult.Fail("Record was not found.");
        }

        if (!isType)
        {
            if (await HasChildTypesAsync(dbConnection, id, cancellationToken))
            {
                return AdminOperationResult.Fail("This session has meal types. Delete or move them first.");
            }

            if (await IsSessionInUseAsync(dbConnection, id, cancellationToken))
            {
                return AdminOperationResult.Fail("This session is used by meal items or combos.");
            }
        }
        else if (await IsTypeInUseAsync(dbConnection, id, cancellationToken))
        {
            return AdminOperationResult.Fail("This meal type is used by meal items or combos.");
        }

        try
        {
            var parentFilter = isType ? "ParentId IS NOT NULL" : "ParentId IS NULL";
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    $"""
                    DELETE FROM Enums
                    WHERE Id = @Id
                      AND EnumTypeId = @EnumTypeId
                      AND {parentFilter};
                    """,
                    new { Id = id, EnumTypeId = MealTypeEnumTypeId },
                    cancellationToken: cancellationToken));
            return rows > 0
                ? AdminOperationResult.Ok("Record deleted successfully.")
                : AdminOperationResult.Fail("Record was not deleted.");
        }
        catch
        {
            return AdminOperationResult.Fail("Record could not be deleted. It may be in use.");
        }
    }

    public async Task<IReadOnlyList<MealSessionListItemDto>> ListSessionsAsync(
        bool activeOnly = false,
        int? includeSessionId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var activeFilter = activeOnly
            ? "AND (ISNULL(IsActive, 1) = 1 OR (@IncludeSessionId IS NOT NULL AND Id = @IncludeSessionId))"
            : string.Empty;

        var rows = await dbConnection.QueryAsync<MealSessionListItemDto>(
            new CommandDefinition(
                $"""
                SELECT Id,
                       EnumValue AS Name,
                       ISNULL(SortOrder, 0) AS SortOrder,
                       ISNULL(IsActive, 1) AS IsActive
                FROM Enums
                WHERE EnumTypeId = @EnumTypeId
                  AND ParentId IS NULL
                  {activeFilter}
                ORDER BY SortOrder, EnumValue;
                """,
                new
                {
                    EnumTypeId = MealTypeEnumTypeId,
                    IncludeSessionId = includeSessionId is > 0 ? includeSessionId : null
                },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private async Task<AdminOperationResult> InsertAsync(
        DbConnection dbConnection,
        MealTypeSaveRequest request,
        string name,
        int? parentId,
        bool isType,
        CancellationToken cancellationToken)
    {
        var nextId = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT ISNULL(MAX(Id), 0) + 1 FROM Enums;",
                cancellationToken: cancellationToken));

        var inserted = await dbConnection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO Enums (Id, EnumValue, Description, EnumTypeId, ParentId, SortOrder, IsDeletable, IsEditable, IsActive, CreatedBy, CreatedOn)
                VALUES (@Id, @Name, @Name, @EnumTypeId, @ParentId, @SortOrder, 1, 1, @IsActive, @CreatedBy, GETUTCDATE());
                """,
                new
                {
                    Id = nextId,
                    Name = name,
                    ParentId = parentId,
                    request.SortOrder,
                    request.IsActive,
                    request.CreatedBy,
                    EnumTypeId = MealTypeEnumTypeId
                },
                cancellationToken: cancellationToken));

        var label = isType ? "Meal type" : "Meal session";
        return inserted > 0
            ? AdminOperationResult.Ok($"{label} added successfully.")
            : AdminOperationResult.Fail($"{label} was not added.");
    }

    private async Task<AdminOperationResult> UpdateAsync(
        DbConnection dbConnection,
        MealTypeSaveRequest request,
        string name,
        int? parentId,
        bool isType,
        CancellationToken cancellationToken)
    {
        if (!await RowExistsAsync(dbConnection, request.Id, isType, cancellationToken))
        {
            return AdminOperationResult.Fail("Record was not found.");
        }

        using var tx = await dbConnection.BeginTransactionAsync(cancellationToken);
        try
        {
            int? previousParentId = null;
            if (isType)
            {
                previousParentId = await dbConnection.ExecuteScalarAsync<int?>(
                    new CommandDefinition(
                        """
                        SELECT ParentId
                        FROM Enums
                        WHERE Id = @Id AND EnumTypeId = @EnumTypeId AND ParentId IS NOT NULL;
                        """,
                        new { request.Id, EnumTypeId = MealTypeEnumTypeId },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE Enums
                    SET EnumValue = @Name,
                        Description = @Name,
                        ParentId = @ParentId,
                        SortOrder = @SortOrder,
                        IsActive = @IsActive,
                        UpdatedOn = GETUTCDATE(),
                        UpdatedBy = @UpdatedBy
                    WHERE Id = @Id AND EnumTypeId = @EnumTypeId;
                    """,
                    new
                    {
                        request.Id,
                        Name = name,
                        ParentId = parentId,
                        request.SortOrder,
                        request.IsActive,
                        request.UpdatedBy,
                        EnumTypeId = MealTypeEnumTypeId
                    },
                    transaction: tx,
                    cancellationToken: cancellationToken));

            if (rows == 0)
            {
                await tx.RollbackAsync(cancellationToken);
                var label = isType ? "Meal type" : "Meal session";
                return AdminOperationResult.Fail($"{label} was not updated.");
            }

            if (isType && parentId is > 0 && previousParentId != parentId)
            {
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        "UPDATE MealItem SET MealSessionId = @ParentId WHERE MealTypeId = @Id;",
                        new { request.Id, ParentId = parentId.Value },
                        transaction: tx,
                        cancellationToken: cancellationToken));
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        "UPDATE MealPackages SET MealSessionId = @ParentId WHERE MealTypeId = @Id;",
                        new { request.Id, ParentId = parentId.Value },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            await tx.CommitAsync(cancellationToken);
            var successLabel = isType ? "Meal type" : "Meal session";
            return AdminOperationResult.Ok($"{successLabel} updated successfully.");
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            var label = isType ? "Meal type" : "Meal session";
            return AdminOperationResult.Fail($"{label} was not updated.");
        }
    }

    private static async Task<bool> SessionExistsAsync(
        DbConnection dbConnection,
        int sessionId,
        CancellationToken cancellationToken) =>
        await dbConnection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM Enums
                    WHERE Id = @SessionId
                      AND EnumTypeId = @EnumTypeId
                      AND ParentId IS NULL
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;
                """,
                new { SessionId = sessionId, EnumTypeId = MealTypeEnumTypeId },
                cancellationToken: cancellationToken));

    private static async Task<bool> NameExistsAsync(
        DbConnection dbConnection,
        string name,
        int? parentId,
        int excludeId,
        CancellationToken cancellationToken) =>
        await dbConnection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM Enums
                    WHERE EnumTypeId = @EnumTypeId
                      AND LTRIM(RTRIM(EnumValue)) = @Name
                      AND (
                            (@ParentId IS NULL AND ParentId IS NULL)
                            OR ParentId = @ParentId
                          )
                      AND (@ExcludeId = 0 OR Id <> @ExcludeId)
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;
                """,
                new
                {
                    EnumTypeId = MealTypeEnumTypeId,
                    Name = name,
                    ParentId = parentId,
                    ExcludeId = excludeId
                },
                cancellationToken: cancellationToken));

    private static async Task<bool> RowExistsAsync(
        DbConnection dbConnection,
        int id,
        bool isType,
        CancellationToken cancellationToken)
    {
        var parentFilter = isType ? "ParentId IS NOT NULL" : "ParentId IS NULL";
        return await dbConnection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                $"""
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM Enums
                    WHERE Id = @Id
                      AND EnumTypeId = @EnumTypeId
                      AND {parentFilter}
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;
                """,
                new { Id = id, EnumTypeId = MealTypeEnumTypeId },
                cancellationToken: cancellationToken));
    }

    private static async Task<bool> HasChildTypesAsync(
        DbConnection dbConnection,
        int sessionId,
        CancellationToken cancellationToken) =>
        await dbConnection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM Enums
                    WHERE EnumTypeId = @EnumTypeId
                      AND ParentId = @SessionId
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;
                """,
                new { EnumTypeId = MealTypeEnumTypeId, SessionId = sessionId },
                cancellationToken: cancellationToken));

    private static async Task<bool> IsSessionInUseAsync(
        DbConnection dbConnection,
        int sessionId,
        CancellationToken cancellationToken) =>
        await dbConnection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM MealItem WHERE MealSessionId = @SessionId AND ISNULL(IsDeleted, 0) = 0
                    UNION ALL
                    SELECT 1 FROM MealPackages WHERE MealSessionId = @SessionId AND ISNULL(IsDeleted, 0) = 0
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;
                """,
                new { SessionId = sessionId },
                cancellationToken: cancellationToken));

    private static async Task<bool> IsTypeInUseAsync(
        DbConnection dbConnection,
        int mealTypeId,
        CancellationToken cancellationToken) =>
        await dbConnection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM MealItem WHERE MealTypeId = @MealTypeId AND ISNULL(IsDeleted, 0) = 0
                    UNION ALL
                    SELECT 1 FROM MealPackages WHERE MealTypeId = @MealTypeId AND ISNULL(IsDeleted, 0) = 0
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;
                """,
                new { MealTypeId = mealTypeId },
                cancellationToken: cancellationToken));
}
