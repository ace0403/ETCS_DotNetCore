using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealCombos;

public sealed class MealComboAdminRepository : IMealComboAdminRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;
    private readonly IMealEnumAdminRepository _mealEnumAdminRepository;

    public MealComboAdminRepository(
        IMealDbConnectionFactory connectionFactory,
        IMealEnumAdminRepository mealEnumAdminRepository)
    {
        _connectionFactory = connectionFactory;
        _mealEnumAdminRepository = mealEnumAdminRepository;
    }

    private const string SelectSql = "SELECT p.Id, p.PackageName, p.SchoolId, p.Price, ISNULL(p.ProcessingFee, 0) AS ProcessingFee, ISNULL(p.IsActive, 1) AS IsActive";
    private const string FromSql = "FROM MealPackages p";
    private const string BaseFilterSql = "ISNULL(p.IsDeleted, 0) = 0";
    private const string SearchFilterSql = "LTRIM(RTRIM(ISNULL(p.PackageName, ''))) LIKE '%' + @Search + '%' OR CAST(p.SchoolId AS varchar(20)) LIKE '%' + @Search + '%' OR CAST(p.Price AS varchar(30)) LIKE '%' + @Search + '%'";

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Id"] = "p.Id",
        ["PackageName"] = "p.PackageName",
        ["SchoolId"] = "p.SchoolId",
        ["Price"] = "p.Price",
        ["ProcessingFee"] = "p.ProcessingFee",
        ["IsActive"] = "p.IsActive"
    };

    public async Task<DataTableResponse<MealComboListDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var baseFilterSql = BaseFilterSql;
        object? extraParameters = null;
        if (request.ScopedSchoolIds is { Count: > 0 })
        {
            baseFilterSql += " AND p.SchoolId IN @ScopedSchoolIds";
            extraParameters = new { ScopedSchoolIds = request.ScopedSchoolIds };
        }
        else if (request.SchoolId is > 0)
        {
            baseFilterSql += " AND p.SchoolId = @SchoolId";
            extraParameters = new { SchoolId = request.SchoolId.Value };
        }

        return await QueryPagedAsync<MealComboListDto>(
            dbConnection,
            SelectSql,
            FromSql,
            baseFilterSql,
            SearchFilterSql,
            SortColumns,
            "p.PackageName",
            request,
            extraParameters,
            cancellationToken: cancellationToken);
    }

    public async Task<MealComboSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return null;
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var package = await dbConnection.QuerySingleOrDefaultAsync<MealComboSaveRequest>(
            new CommandDefinition(
                """
                SELECT Id, SchoolId, MealSessionId, MealTypeId, MealCategotyId AS MealCategoryId, PackageName, Detail, Price,
                    ISNULL(ProcessingFee, 0) AS ProcessingFee, ImageName, ISNULL(IsActive, 1) AS IsActive
                FROM MealPackages WHERE Id = @Id;
                """,
                new { Id = id },
                cancellationToken: cancellationToken));

        if (package is null) return null;

        package.WeekNos = (await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                "SELECT WeekNo FROM MealPackageWeeks WHERE MealPackageId = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken))).ToList();

        package.DayIds = (await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                "SELECT DayId FROM MealPackageDays WHERE MealPackageId = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken))).ToList();

        package.IngredientIds = (await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                "SELECT IngredientId FROM MealPackageIngredients WHERE MealPackageId = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken))).ToList();

        package.NutritionLines = (await dbConnection.QueryAsync<MealItemNutritionLineDto>(
            new CommandDefinition(
                """
                SELECT NutritionId, MeasureValue, MeasureTypeId
                FROM MealPackageNutrition WHERE MealPackageId = @Id;
                """,
                new { Id = id },
                cancellationToken: cancellationToken))).ToList();

        return package;
    }

    public async Task<AdminOperationResult> SaveAsync(MealComboSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.WeekNos is null || request.WeekNos.Count == 0)
            return AdminOperationResult.Fail("Select at least one week.");

        if (request.DayIds is null || request.DayIds.Count == 0)
            return AdminOperationResult.Fail("Select at least one day.");

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

        var nutritionLines = request.NutritionLines?
            .Where(n => n.NutritionId > 0 && n.MeasureTypeId > 0)
            .ToList() ?? [];

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        using var tx = await dbConnection.BeginTransactionAsync(cancellationToken);

        try
        {
            int packageId = request.Id;
            if (request.Id > 0)
            {
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        UPDATE MealPackages
                        SET SchoolId = @SchoolId, MealSessionId = @MealSessionId, MealTypeId = @MealTypeId, MealCategotyId = @MealCategoryId,
                            PackageName = @PackageName, Detail = @Detail, Price = @Price, ProcessingFee = @ProcessingFee,
                            ImageName = CASE WHEN @ImageName IS NOT NULL AND @ImageName <> '' THEN @ImageName ELSE ImageName END,
                            IsActive = @IsActive, UpdatedOn = GETUTCDATE(), UpdatedBy = @UpdatedBy
                        WHERE Id = @Id;
                        """,
                        request,
                        transaction: tx,
                        cancellationToken: cancellationToken));

                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        DELETE FROM MealPackageWeeks WHERE MealPackageId = @Id;
                        DELETE FROM MealPackageDays WHERE MealPackageId = @Id;
                        DELETE FROM MealPackageIngredients WHERE MealPackageId = @Id;
                        DELETE FROM MealPackageNutrition WHERE MealPackageId = @Id;
                        """,
                        new { request.Id },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }
            else
            {
                packageId = await dbConnection.ExecuteScalarAsync<int>(
                    new CommandDefinition(
                        """
                        INSERT INTO MealPackages (SchoolId, MealSessionId, MealTypeId, MealCategotyId, PackageName, Detail, Price, ProcessingFee, ImageName,
                            IsActive, IsDeleted, CreatedBy, CreatedOn)
                        VALUES (@SchoolId, @MealSessionId, @MealTypeId, @MealCategoryId, @PackageName, @Detail, @Price, @ProcessingFee, ISNULL(@ImageName, ''),
                            @IsActive, 0, @CreatedBy, GETUTCDATE());
                        SELECT CAST(SCOPE_IDENTITY() AS INT);
                        """,
                        request,
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            foreach (var weekNo in request.WeekNos.Distinct())
            {
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        "INSERT INTO MealPackageWeeks (MealPackageId, WeekNo, CreatedOn) VALUES (@PackageId, @WeekNo, GETUTCDATE());",
                        new { PackageId = packageId, WeekNo = weekNo },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            foreach (var dayId in request.DayIds.Distinct())
            {
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        "INSERT INTO MealPackageDays (MealPackageId, DayId, CreatedOn) VALUES (@PackageId, @DayId, GETUTCDATE());",
                        new { PackageId = packageId, DayId = dayId },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            foreach (var ingredientId in (request.IngredientIds ?? []).Where(id => id > 0).Distinct())
            {
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        "INSERT INTO MealPackageIngredients (MealPackageId, IngredientId, CreatedOn) " +
                        "VALUES (@PackageId, @IngredientId, GETUTCDATE());",
                        new { PackageId = packageId, IngredientId = ingredientId },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            foreach (var line in nutritionLines)
            {
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO MealPackageNutrition (MealPackageId, NutritionId, MeasureValue, MeasureTypeId, CreatedBy, CreatedOn)
                        VALUES (@PackageId, @NutritionId, @MeasureValue, @MeasureTypeId, @CreatedBy, GETUTCDATE());
                        """,
                        new
                        {
                            PackageId = packageId,
                            line.NutritionId,
                            line.MeasureValue,
                            line.MeasureTypeId,
                            request.CreatedBy
                        },
                        transaction: tx,
                        cancellationToken: cancellationToken));
            }

            await tx.CommitAsync(cancellationToken);
            return AdminOperationResult.Ok(request.Id > 0 ? "Combo updated successfully." : "Combo added successfully.");
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            return AdminOperationResult.Fail("Combo could not be saved.");
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
                "UPDATE MealPackages SET IsDeleted = 1, IsActive = 0 WHERE Id = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken));
        return rows > 0
            ? AdminOperationResult.Ok("Record deleted successfully.")
            : AdminOperationResult.Fail("Record was not deleted.");
    }
}
