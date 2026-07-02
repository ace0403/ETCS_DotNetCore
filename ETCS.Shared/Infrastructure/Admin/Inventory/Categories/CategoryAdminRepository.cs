using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.Categories;

public sealed class CategoryAdminRepository : ICategoryAdminRepository
{
    private const int MealCategoryEnumTypeId = 10;

    private readonly IMealDbConnectionFactory _connectionFactory;

    public CategoryAdminRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectSql = """
        SELECT Id,
               EnumValue AS CategoryName,
               ISNULL(SortOrder, 0) AS SortOrder,
               ISNULL(IsActive, 1) AS IsActive
        """;
    private const string FromSql = "FROM Enums";
    private const string BaseFilterSql = "EnumTypeId = @EnumTypeId";
    private const string SearchFilterSql = "LTRIM(RTRIM(ISNULL(EnumValue, ''))) LIKE '%' + @Search + '%'";

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Id"] = "Id",
        ["CategoryName"] = "EnumValue",
        ["SortOrder"] = "SortOrder",
        ["IsActive"] = "IsActive"
    };

    public async Task<DataTableResponse<CategoryListItemDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await QueryPagedAsync<CategoryListItemDto>(
            dbConnection,
            SelectSql,
            FromSql,
            BaseFilterSql,
            SearchFilterSql,
            SortColumns,
            "SortOrder, EnumValue",
            request,
            new { EnumTypeId = MealCategoryEnumTypeId },
            cancellationToken);
    }

    public async Task<IReadOnlyList<CategoryListItemDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<CategoryListItemDto>(
            new CommandDefinition(
                """
                SELECT Id, EnumValue AS CategoryName, ISNULL(SortOrder, 0) AS SortOrder, ISNULL(IsActive, 1) AS IsActive
                FROM Enums
                WHERE EnumTypeId = @EnumTypeId
                ORDER BY SortOrder, EnumValue;
                """,
                new { EnumTypeId = MealCategoryEnumTypeId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<CategorySaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return null;
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await dbConnection.QuerySingleOrDefaultAsync<CategorySaveRequest>(
            new CommandDefinition(
                """
                SELECT Id, EnumValue AS CategoryName, ISNULL(SortOrder, 0) AS SortOrder, ISNULL(IsActive, 1) AS IsActive
                FROM Enums WHERE Id = @Id AND EnumTypeId = @EnumTypeId;
                """,
                new { Id = id, EnumTypeId = MealCategoryEnumTypeId },
                cancellationToken: cancellationToken));
    }

    public async Task<AdminOperationResult> SaveAsync(CategorySaveRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        if (request.Id > 0)
        {
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE Enums
                    SET EnumValue = @CategoryName,
                        Description = @CategoryName,
                        SortOrder = @SortOrder,
                        IsActive = @IsActive,
                        UpdatedOn = GETUTCDATE(),
                        UpdatedBy = @UpdatedBy
                    WHERE Id = @Id AND EnumTypeId = @EnumTypeId;
                    """,
                    new
                    {
                        request.Id,
                        request.CategoryName,
                        request.SortOrder,
                        request.IsActive,
                        request.UpdatedBy,
                        EnumTypeId = MealCategoryEnumTypeId
                    },
                    cancellationToken: cancellationToken));
            return rows > 0
                ? AdminOperationResult.Ok("Category updated successfully.")
                : AdminOperationResult.Fail("Category was not updated.");
        }

        var nextId = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT ISNULL(MAX(Id), 0) + 1 FROM Enums;", cancellationToken: cancellationToken));

        var inserted = await dbConnection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO Enums (Id, EnumValue, Description, EnumTypeId, SortOrder, IsDeletable, IsEditable, IsActive, CreatedBy, CreatedOn)
                VALUES (@Id, @CategoryName, @CategoryName, @EnumTypeId, @SortOrder, 1, 1, @IsActive, @CreatedBy, GETUTCDATE());
                """,
                new
                {
                    Id = nextId,
                    request.CategoryName,
                    request.SortOrder,
                    request.IsActive,
                    request.CreatedBy,
                    EnumTypeId = MealCategoryEnumTypeId
                },
                cancellationToken: cancellationToken));
        return inserted > 0
            ? AdminOperationResult.Ok("Category added successfully.")
            : AdminOperationResult.Fail("Category was not added.");
    }

    public async Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return AdminOperationResult.Fail("Id is required.");
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        try
        {
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM Enums WHERE Id = @Id AND EnumTypeId = @EnumTypeId;",
                    new { Id = id, EnumTypeId = MealCategoryEnumTypeId },
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
}
