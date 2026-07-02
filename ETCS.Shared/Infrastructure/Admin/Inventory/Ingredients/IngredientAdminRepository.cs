using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.Ingredients;

public sealed class IngredientAdminRepository : IIngredientAdminRepository
{
    private const int IngredientEnumTypeId = MealEnumTypeIds.FoodAllergy;

    private readonly IMealDbConnectionFactory _connectionFactory;

    public IngredientAdminRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectSql = """
        SELECT Id,
               EnumValue AS IngredientName,
               ISNULL(SortOrder, 0) AS SortOrder,
               ISNULL(IsActive, 1) AS IsActive
        """;

    private const string FromSql = "FROM Enums";

    private const string BaseFilterSql = "EnumTypeId = @EnumTypeId";

    private const string SearchFilterSql = """
        (
            LTRIM(RTRIM(ISNULL(EnumValue, ''))) LIKE '%' + @Search + '%'
            OR LTRIM(RTRIM(ISNULL(Description, ''))) LIKE '%' + @Search + '%'
        )
        """;

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Id"] = "Id",
        ["IngredientName"] = "EnumValue",
        ["SortOrder"] = "SortOrder",
        ["IsActive"] = "IsActive"
    };

    public async Task<DataTableResponse<IngredientListItemDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await QueryPagedAsync<IngredientListItemDto>(
            dbConnection,
            SelectSql,
            FromSql,
            BaseFilterSql,
            SearchFilterSql,
            SortColumns,
            "SortOrder, EnumValue",
            request,
            new { EnumTypeId = IngredientEnumTypeId },
            cancellationToken);
    }

    public async Task<IReadOnlyList<IngredientListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<IngredientListItemDto>(
            new CommandDefinition(
                """
                SELECT Id,
                       EnumValue AS IngredientName,
                       ISNULL(SortOrder, 0) AS SortOrder,
                       ISNULL(IsActive, 1) AS IsActive
                FROM Enums
                WHERE EnumTypeId = @EnumTypeId
                ORDER BY SortOrder, EnumValue;
                """,
                new { EnumTypeId = IngredientEnumTypeId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IngredientSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await dbConnection.QuerySingleOrDefaultAsync<IngredientSaveRequest>(
            new CommandDefinition(
                """
                SELECT Id,
                       EnumValue AS IngredientName,
                       Description,
                       ISNULL(SortOrder, 0) AS SortOrder,
                       ISNULL(IsActive, 1) AS IsActive
                FROM Enums
                WHERE Id = @Id AND EnumTypeId = @EnumTypeId;
                """,
                new { Id = id, EnumTypeId = IngredientEnumTypeId },
                cancellationToken: cancellationToken));
    }

    public async Task<AdminOperationResult> SaveAsync(IngredientSaveRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? request.IngredientName
            : request.Description.Trim();

        if (request.Id > 0)
        {
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE Enums
                    SET EnumValue = @IngredientName,
                        Description = @Description,
                        SortOrder = @SortOrder,
                        IsActive = @IsActive,
                        UpdatedOn = GETUTCDATE(),
                        UpdatedBy = @UpdatedBy
                    WHERE Id = @Id AND EnumTypeId = @EnumTypeId;
                    """,
                    new
                    {
                        request.Id,
                        request.IngredientName,
                        Description = description,
                        request.SortOrder,
                        request.IsActive,
                        request.UpdatedBy,
                        EnumTypeId = IngredientEnumTypeId
                    },
                    cancellationToken: cancellationToken));
            return rows > 0
                ? AdminOperationResult.Ok("Ingredient updated successfully.")
                : AdminOperationResult.Fail("Ingredient was not updated.");
        }

        var nextId = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT ISNULL(MAX(Id), 0) + 1 FROM Enums;", cancellationToken: cancellationToken));

        var inserted = await dbConnection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO Enums (Id, EnumValue, Description, EnumTypeId, SortOrder, IsDeletable, IsEditable, IsActive, CreatedBy, CreatedOn)
                VALUES (@Id, @IngredientName, @Description, @EnumTypeId, @SortOrder, 1, 1, @IsActive, @CreatedBy, GETUTCDATE());
                """,
                new
                {
                    Id = nextId,
                    request.IngredientName,
                    Description = description,
                    request.SortOrder,
                    request.IsActive,
                    request.CreatedBy,
                    EnumTypeId = IngredientEnumTypeId
                },
                cancellationToken: cancellationToken));
        return inserted > 0
            ? AdminOperationResult.Ok("Ingredient added successfully.")
            : AdminOperationResult.Fail("Ingredient was not added.");
    }

    public async Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return AdminOperationResult.Fail("Id is required.");
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var mealItemUsage = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM MealItemIngredients WHERE IngredientId = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken));

        if (mealItemUsage > 0)
        {
            return AdminOperationResult.Fail("Cannot delete — ingredient is assigned to meal items or student allergies.");
        }

        var studentAllergyUsage = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM StudentAllergies WHERE AllergyItemId = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken));

        if (studentAllergyUsage > 0)
        {
            return AdminOperationResult.Fail("Cannot delete — ingredient is assigned to meal items or student allergies.");
        }

        var rows = await dbConnection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM Enums WHERE Id = @Id AND EnumTypeId = @EnumTypeId;",
                new { Id = id, EnumTypeId = IngredientEnumTypeId },
                cancellationToken: cancellationToken));

        return rows > 0
            ? AdminOperationResult.Ok("Record deleted successfully.")
            : AdminOperationResult.Fail("Record was not deleted.");
    }
}
