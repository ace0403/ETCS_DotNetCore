using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;

public sealed class MealItemAdminRepository : IMealItemAdminRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public MealItemAdminRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectSql = """
        SELECT mi.Id,
            LTRIM(RTRIM(ISNULL(mi.ItemName, ''))) AS ItemName,
            LTRIM(RTRIM(ISNULL(mc.EnumValue, ''))) AS CategoryName,
            mi.SchoolId,
            mi.MealTypeId,
            mi.MealCategotyId AS MealCategoryId,
            mi.Price,
            ISNULL(mi.IsActive, 1) AS IsActive
        """;
    private const string FromSql = "FROM MealItem mi LEFT JOIN Enums mc ON mi.MealCategotyId = mc.Id";
    private const string BaseFilterSql = "ISNULL(mi.IsDeleted, 0) = 0";
    private const string SearchFilterSql = """
        LTRIM(RTRIM(ISNULL(mi.ItemName, ''))) LIKE '%' + @Search + '%'
        OR LTRIM(RTRIM(ISNULL(mc.EnumValue, ''))) LIKE '%' + @Search + '%'
        OR CAST(mi.SchoolId AS varchar(20)) LIKE '%' + @Search + '%'
        OR CAST(mi.MealTypeId AS varchar(20)) LIKE '%' + @Search + '%'
        OR CAST(mi.MealCategotyId AS varchar(20)) LIKE '%' + @Search + '%'
        OR CAST(mi.Price AS varchar(30)) LIKE '%' + @Search + '%'
        """;

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Id"] = "mi.Id",
        ["ItemName"] = "mi.ItemName",
        ["CategoryName"] = "mc.EnumValue",
        ["SchoolId"] = "mi.SchoolId",
        ["MealTypeId"] = "mi.MealTypeId",
        ["MealCategoryId"] = "mi.MealCategotyId",
        ["Price"] = "mi.Price",
        ["IsActive"] = "mi.IsActive"
    };

    public async Task<DataTableResponse<MealItemListDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var baseFilterSql = BaseFilterSql;
        object? extraParameters = null;
        if (request.SchoolId is > 0)
        {
            baseFilterSql += " AND mi.SchoolId = @SchoolId";
            extraParameters = new { SchoolId = request.SchoolId.Value };
        }

        return await QueryPagedAsync<MealItemListDto>(
            dbConnection,
            SelectSql,
            FromSql,
            baseFilterSql,
            SearchFilterSql,
            SortColumns,
            "mi.ItemName",
            request,
            extraParameters,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<MealItemListDto>> ListBySchoolAsync(int schoolId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<MealItemListDto>(
            new CommandDefinition(
                """
                SELECT Id, ItemName, SchoolId, MealTypeId, MealCategotyId AS MealCategoryId, Price, ISNULL(IsActive, 1) AS IsActive
                FROM MealItem
                WHERE SchoolId = @SchoolId AND ISNULL(IsDeleted, 0) = 0 AND ISNULL(IsActive, 1) = 1
                ORDER BY ItemName;
                """,
                new { SchoolId = schoolId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<MealItemSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return null;
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var item = await dbConnection.QuerySingleOrDefaultAsync<MealItemSaveRequest>(
            new CommandDefinition(
                """
                SELECT Id, SchoolId, MealTypeId, MealCategotyId AS MealCategoryId, ItemName, Detail, Price,
                    ImageName, ISNULL(IsActive, 1) AS IsActive
                FROM MealItem WHERE Id = @Id;
                """,
                new { Id = id },
                cancellationToken: cancellationToken));

        if (item is null) return null;

        item.IngredientIds = (await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                "SELECT IngredientId FROM MealItemIngredients WHERE MealItemId = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken))).ToList();

        item.WeekNos = (await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                "SELECT WeekNo FROM MealItemWeeks WHERE MealItemId = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken))).ToList();

        item.DayIds = (await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                "SELECT DayId FROM MealItemDays WHERE MealItemId = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken))).ToList();

        item.NutritionLines = (await dbConnection.QueryAsync<MealItemNutritionLineDto>(
            new CommandDefinition(
                """
                SELECT NutritionId, MeasureValue, MeasureTypeId
                FROM MealItemNutrition WHERE MealItemId = @Id;
                """,
                new { Id = id },
                cancellationToken: cancellationToken))).ToList();

        return item;
    }

    public async Task<AdminOperationResult> SaveAsync(MealItemSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.WeekNos is null || request.WeekNos.Count == 0)
            return AdminOperationResult.Fail("Select at least one week.");

        if (request.DayIds is null || request.DayIds.Count == 0)
            return AdminOperationResult.Fail("Select at least one day.");

        var nutritionLines = request.NutritionLines?
            .Where(n => n.NutritionId > 0 && n.MeasureTypeId > 0)
            .ToList() ?? [];

        if (nutritionLines.Count == 0)
            return AdminOperationResult.Fail("Add at least one nutrition row.");

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        using var tx = await dbConnection.BeginTransactionAsync(cancellationToken);

        try
        {
            int itemId = request.Id;
            if (request.Id > 0)
            {
                var rows = await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        UPDATE MealItem
                        SET SchoolId = @SchoolId, MealTypeId = @MealTypeId, MealCategotyId = @MealCategoryId,
                            ItemName = @ItemName, Detail = @Detail, Price = @Price,
                            ImageName = CASE WHEN @ImageName IS NOT NULL AND @ImageName <> '' THEN @ImageName ELSE ImageName END,
                            IsActive = @IsActive, UpdatedOn = GETUTCDATE(), UpdatedBy = @UpdatedBy
                        WHERE Id = @Id;
                        """,
                        request,
                        transaction: tx,
                        cancellationToken: cancellationToken));

                if (rows == 0)
                {
                    await tx.RollbackAsync(cancellationToken);
                    return AdminOperationResult.Fail("Item was not updated.");
                }

                await DeleteChildRowsAsync(dbConnection, tx, itemId, cancellationToken);
            }
            else
            {
                itemId = await dbConnection.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        """
                        INSERT INTO MealItem (SchoolId, MealTypeId, MealCategotyId, ItemName, Detail, Price, ImageName,
                            IsActive, IsDeleted, CreatedBy, CreatedOn)
                        VALUES (@SchoolId, @MealTypeId, @MealCategoryId, @ItemName, @Detail, @Price, ISNULL(@ImageName, ''),
                            @IsActive, 0, @CreatedBy, GETUTCDATE());
                        SELECT CAST(SCOPE_IDENTITY() AS INT);
                        """,
                        request,
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            foreach (var ingredientId in (request.IngredientIds ?? []).Distinct())
            {
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        "INSERT INTO MealItemIngredients (MealItemId, IngredientId, CreatedOn) VALUES (@ItemId, @IngredientId, GETUTCDATE());",
                        new { ItemId = itemId, IngredientId = ingredientId },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            foreach (var weekNo in request.WeekNos.Distinct())
            {
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        "INSERT INTO MealItemWeeks (MealItemId, WeekNo, CreatedOn) VALUES (@ItemId, @WeekNo, GETUTCDATE());",
                        new { ItemId = itemId, WeekNo = weekNo },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            foreach (var dayId in request.DayIds.Distinct())
            {
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        "INSERT INTO MealItemDays (MealItemId, DayId, CreatedOn) VALUES (@ItemId, @DayId, GETUTCDATE());",
                        new { ItemId = itemId, DayId = dayId },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            foreach (var line in nutritionLines)
            {
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO MealItemNutrition (MealItemId, NutritionId, MeasureValue, MeasureTypeId, CreatedBy, CreatedOn)
                        VALUES (@ItemId, @NutritionId, @MeasureValue, @MeasureTypeId, @CreatedBy, GETUTCDATE());
                        """,
                        new
                        {
                            ItemId = itemId,
                            line.NutritionId,
                            line.MeasureValue,
                            line.MeasureTypeId,
                            request.CreatedBy
                        },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            await tx.CommitAsync(cancellationToken);
            return AdminOperationResult.Ok(request.Id > 0 ? "Item updated successfully." : "Item added successfully.");
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return AdminOperationResult.Fail("Item could not be saved.");
        }
    }

    public async Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return AdminOperationResult.Fail("Id is required.");
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE MealItem SET IsDeleted = 1, IsActive = 0 WHERE Id = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken));
        return rows > 0
            ? AdminOperationResult.Ok("Record deleted successfully.")
            : AdminOperationResult.Fail("Record was not deleted.");
    }

    private static async Task DeleteChildRowsAsync(
        DbConnection dbConnection,
        DbTransaction tx,
        int itemId,
        CancellationToken cancellationToken)
    {
        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM MealItemIngredients WHERE MealItemId = @ItemId;
                DELETE FROM MealItemWeeks WHERE MealItemId = @ItemId;
                DELETE FROM MealItemDays WHERE MealItemId = @ItemId;
                DELETE FROM MealItemNutrition WHERE MealItemId = @ItemId;
                """,
                new { ItemId = itemId },
                transaction: tx,
                cancellationToken: cancellationToken));
    }
}
