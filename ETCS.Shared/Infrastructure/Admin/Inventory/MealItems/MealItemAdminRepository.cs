using System.Data.Common;
using Dapper;
using ETCS.Shared.Application.Students;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;

public sealed class MealItemAdminRepository : IMealItemAdminRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;
    private readonly IMealItemOrderTypeAdminRepository _orderTypeRepository;
    private readonly IMealItemSchoolAdminRepository _schoolRepository;
    private readonly IMealEnumAdminRepository _mealEnumAdminRepository;

    public MealItemAdminRepository(
        IMealDbConnectionFactory connectionFactory,
        IMealItemOrderTypeAdminRepository orderTypeRepository,
        IMealItemSchoolAdminRepository schoolRepository,
        IMealEnumAdminRepository mealEnumAdminRepository)
    {
        _connectionFactory = connectionFactory;
        _orderTypeRepository = orderTypeRepository;
        _schoolRepository = schoolRepository;
        _mealEnumAdminRepository = mealEnumAdminRepository;
    }

    private const string SelectSql = """
        SELECT mi.Id,
            LTRIM(RTRIM(ISNULL(mi.ItemName, ''))) AS ItemName,
            LTRIM(RTRIM(ISNULL(mc.EnumValue, ''))) AS CategoryName,
            LTRIM(RTRIM(ISNULL(schools.SchoolNames, ''))) AS SchoolNames,
            COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(orderTypes.OrderTypeNames, ''))), ''), 'Meal Plans') AS OrderTypeNames,
            mi.SchoolId,
            mi.MealSessionId,
            mi.MealTypeId,
            mi.MealCategotyId AS MealCategoryId,
            mi.Price,
            ISNULL(mi.IsActive, 1) AS IsActive
        """;
    private const string FromSql = """
        FROM MealItem mi
        LEFT JOIN Enums mc ON mi.MealCategotyId = mc.Id
        OUTER APPLY (
            SELECT STRING_AGG(CAST(mis.SchoolId AS varchar(20)), ', ') WITHIN GROUP (ORDER BY mis.SchoolId) AS SchoolNames
            FROM MealItemSchools mis
            WHERE mis.MealItemId = mi.Id
        ) schools
        OUTER APPLY (
            SELECT STRING_AGG(LTRIM(RTRIM(ISNULL(ot.EnumValue, ''))), ', ') WITHIN GROUP (ORDER BY miot.OrderTypeId) AS OrderTypeNames
            FROM MealItemOrderTypes miot
            LEFT JOIN Enums ot ON ot.Id = miot.OrderTypeId
            WHERE miot.MealItemId = mi.Id
        ) orderTypes
        """;
    private const string BaseFilterSql = "ISNULL(mi.IsDeleted, 0) = 0";
    private const string SearchFilterSql = """
        LTRIM(RTRIM(ISNULL(mi.ItemName, ''))) LIKE '%' + @Search + '%'
        OR LTRIM(RTRIM(ISNULL(mc.EnumValue, ''))) LIKE '%' + @Search + '%'
        OR LTRIM(RTRIM(ISNULL(schools.SchoolNames, ''))) LIKE '%' + @Search + '%'
        OR COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(orderTypes.OrderTypeNames, ''))), ''), 'Meal Plans') LIKE '%' + @Search + '%'
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
        ["SchoolNames"] = "schools.SchoolNames",
        ["OrderTypeNames"] = "COALESCE(NULLIF(LTRIM(RTRIM(ISNULL(orderTypes.OrderTypeNames, ''))), ''), 'Meal Plans')",
        ["SchoolId"] = "mi.SchoolId",
        ["MealSessionId"] = "mi.MealSessionId",
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
        var parameters = new DynamicParameters();
        if (request.SchoolId is > 0)
        {
            baseFilterSql += """
                 AND (
                     EXISTS (
                         SELECT 1
                         FROM MealItemSchools mis
                         WHERE mis.MealItemId = mi.Id
                           AND mis.SchoolId = @SchoolId
                     )
                     OR (
                         NOT EXISTS (SELECT 1 FROM MealItemSchools mis2 WHERE mis2.MealItemId = mi.Id)
                         AND mi.SchoolId = @SchoolId
                     )
                 )
                """;
            parameters.Add("SchoolId", request.SchoolId.Value);
        }

        if (request.OrderTypeId is > 0)
        {
            baseFilterSql += """
                   AND EXISTS (
                       SELECT 1
                       FROM MealItemOrderTypes miot
                       WHERE miot.MealItemId = mi.Id
                         AND miot.OrderTypeId = @OrderTypeId
                   )
                  """;
            parameters.Add("OrderTypeId", request.OrderTypeId.Value);
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
            parameters,
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
                SELECT mi.Id, mi.ItemName, mi.SchoolId, mi.MealSessionId, mi.MealTypeId, mi.MealCategotyId AS MealCategoryId, mi.Price, ISNULL(mi.IsActive, 1) AS IsActive
                FROM MealItem mi
                WHERE ISNULL(mi.IsDeleted, 0) = 0
                  AND ISNULL(mi.IsActive, 1) = 1
                  AND (
                      EXISTS (
                          SELECT 1
                          FROM MealItemSchools mis
                          WHERE mis.MealItemId = mi.Id
                            AND mis.SchoolId = @SchoolId
                      )
                      OR (
                          NOT EXISTS (SELECT 1 FROM MealItemSchools mis2 WHERE mis2.MealItemId = mi.Id)
                          AND mi.SchoolId = @SchoolId
                      )
                  )
                  AND EXISTS (
                      SELECT 1
                      FROM MealItemOrderTypes miot
                      WHERE miot.MealItemId = mi.Id
                        AND miot.OrderTypeId = @MealPlanOrderTypeId
                  )
                ORDER BY mi.ItemName;
                """,
                new
                {
                    SchoolId = schoolId,
                    MealPlanOrderTypeId = (int)ETCS.Shared.Enumeration.TransactionTypeEnum.MealOrder
                },
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
                SELECT Id, SchoolId, MealSessionId, MealTypeId, MealCategotyId AS MealCategoryId, ItemName, Detail, Price,
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

        var storedOrderTypeIds = await _orderTypeRepository.GetOrderTypeIdsAsync(id, cancellationToken);
        item.OrderTypeIds = MealItemChannelValidation
            .NormalizeOrderTypeIds(StudentOrderTypeValidation.NormalizeMealItemChannelsForEdit(storedOrderTypeIds))
            .ToList();

        var storedSchoolIds = await _schoolRepository.GetSchoolIdsAsync(id, cancellationToken);
        item.SchoolIds = storedSchoolIds.Count > 0
            ? storedSchoolIds.ToList()
            : item.SchoolId > 0 ? [item.SchoolId] : [];

        return item;
    }

    public async Task<AdminOperationResult> SaveAsync(MealItemSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SchoolIds is null || request.SchoolIds.Count == 0)
            return AdminOperationResult.Fail("Select at least one school.");

        request.SchoolIds = request.SchoolIds.Where(id => id > 0).Distinct().OrderBy(id => id).ToList();
        if (request.SchoolIds.Count == 0)
            return AdminOperationResult.Fail("Select at least one school.");

        request.SchoolId = request.SchoolIds[0];

        if (request.OrderTypeIds is null || request.OrderTypeIds.Count == 0)
            return AdminOperationResult.Fail("Select at least one channel.");

        var channelError = MealItemChannelValidation.ValidateChannelCombination(request.OrderTypeIds);
        if (channelError is not null)
            return AdminOperationResult.Fail(channelError);

        request.OrderTypeIds = MealItemChannelValidation.NormalizeOrderTypeIds(request.OrderTypeIds);

        var isPosOnly = MealItemChannelValidation.IsPosOnly(request.OrderTypeIds);
        if (!isPosOnly)
        {
            if (request.WeekNos is null || request.WeekNos.Count == 0)
                return AdminOperationResult.Fail("Select at least one week.");

            if (request.DayIds is null || request.DayIds.Count == 0)
                return AdminOperationResult.Fail("Select at least one day.");
        }
        else
        {
            request.WeekNos ??= [];
            request.DayIds ??= [];
        }

        var nutritionLines = request.NutritionLines?
            .Where(n => n.NutritionId > 0 && n.MeasureTypeId > 0)
            .ToList() ?? [];

        if (nutritionLines.Count == 0)
            return AdminOperationResult.Fail("Add at least one nutrition row.");

        if (request.MealSessionId <= 0)
            return AdminOperationResult.Fail("Meal session is required.");

        if (request.MealTypeId <= 0)
            return AdminOperationResult.Fail("Meal type is required.");

        if (!await _mealEnumAdminRepository.IsMealTypeInSessionAsync(
                request.MealTypeId,
                request.MealSessionId,
                cancellationToken))
        {
            return AdminOperationResult.Fail("Selected meal type does not belong to the chosen meal session.");
        }

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
                        SET SchoolId = @SchoolId, MealSessionId = @MealSessionId, MealTypeId = @MealTypeId, MealCategotyId = @MealCategoryId,
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
                        INSERT INTO MealItem (SchoolId, MealSessionId, MealTypeId, MealCategotyId, ItemName, Detail, Price, ImageName,
                            IsActive, IsDeleted, CreatedBy, CreatedOn)
                        VALUES (@SchoolId, @MealSessionId, @MealTypeId, @MealCategoryId, @ItemName, @Detail, @Price, ISNULL(@ImageName, ''),
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

            await _orderTypeRepository.SaveOrderTypesAsync(
                itemId,
                request.OrderTypeIds,
                dbConnection,
                tx,
                cancellationToken);

            await _schoolRepository.SaveSchoolIdsAsync(
                itemId,
                request.SchoolIds,
                dbConnection,
                tx,
                cancellationToken);

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

    public async Task<bool> ExistsAsync(
        int schoolId,
        int mealTypeId,
        string itemName,
        int mealCategoryId,
        CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0 || mealTypeId <= 0 || mealCategoryId <= 0 || string.IsNullOrWhiteSpace(itemName))
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
                    FROM MealItem mi
                    WHERE (
                        EXISTS (
                            SELECT 1
                            FROM MealItemSchools mis
                            WHERE mis.MealItemId = mi.Id
                              AND mis.SchoolId = @SchoolId
                        )
                        OR (
                            NOT EXISTS (SELECT 1 FROM MealItemSchools mis2 WHERE mis2.MealItemId = mi.Id)
                            AND mi.SchoolId = @SchoolId
                        )
                    )
                        AND mi.MealTypeId = @MealTypeId
                        AND mi.MealCategotyId = @MealCategoryId
                        AND LTRIM(RTRIM(mi.ItemName)) = @ItemName
                        AND ISNULL(mi.IsDeleted, 0) = 0
                ) THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END;
                """,
                new
                {
                    SchoolId = schoolId,
                    MealTypeId = mealTypeId,
                    MealCategoryId = mealCategoryId,
                    ItemName = itemName.Trim()
                },
                cancellationToken: cancellationToken));
    }

    public async Task<MealItemBulkImportResult> ImportAsync(
        IReadOnlyList<MealItemSaveRequest> items,
        int createdBy,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        var inserted = 0;
        var skippedExisting = 0;
        var failed = 0;

        foreach (var item in items)
        {
            if (item.MealCategoryId is not int mealCategoryId || mealCategoryId <= 0)
            {
                failed++;
                errors.Add($"{item.ItemName}: Menu category is required.");
                continue;
            }

            var importSchoolId = item.SchoolIds.FirstOrDefault();
            if (importSchoolId <= 0 && item.SchoolId > 0)
            {
                importSchoolId = item.SchoolId;
                item.SchoolIds = [item.SchoolId];
            }

            if (await ExistsAsync(importSchoolId, item.MealTypeId, item.ItemName, mealCategoryId, cancellationToken))
            {
                skippedExisting++;
                continue;
            }

            item.Id = 0;
            item.CreatedBy = createdBy;
            item.UpdatedBy = createdBy;

            var result = await SaveAsync(item, cancellationToken);
            if (result.Success)
            {
                inserted++;
            }
            else
            {
                failed++;
                errors.Add($"{item.ItemName}: {result.Message}");
            }
        }

        return new MealItemBulkImportResult
        {
            Inserted = inserted,
            SkippedExisting = skippedExisting,
            Failed = failed,
            Errors = errors
        };
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
