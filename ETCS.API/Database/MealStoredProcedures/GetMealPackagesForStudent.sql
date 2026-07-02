-- Optimized: pre-filter by week/day, aggregated package nutrition in SQL, optional meal type.
-- Run against MealDB after MealMenuPerformanceIndexes.sql.

USE MealDB;
GO

CREATE OR ALTER PROCEDURE [dbo].[GetMealPackagesForStudent]
    @StudentId INT,
    @SchoolId INT,
    @WeekNo INT,
    @DayId INT,
    @MealDate DATETIME,
    @MealTypeId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StudentAllergies TABLE (AllergyItemId INT PRIMARY KEY);
    INSERT INTO @StudentAllergies (AllergyItemId)
    SELECT AllergyItemId
    FROM StudentAllergies
    WHERE StudentId = @StudentId;

    SELECT
        mp.Id,
        mp.PackageName,
        mp.MealTypeId,
        ISNULL(mt.EnumValue, '') AS MealTypeName,
        ISNULL(mt.ClassName, '') AS MealCssClass,
        mp.MealCategotyId AS MealCategoryId,
        ISNULL(mc.EnumValue, '') AS MealCategoryName,
        mp.SchoolId,
        mp.ImageName,
        ISNULL(mp.Detail, '') AS Detail,
        mp.Price,
        ISNULL(mp.ProcessingFee, 0) AS ProcessingFee,
        @MealDate AS CreatedOn,
        items.ItemsName,
        weeks.WeekNo,
        ing.IngredientIds,
        nutr.NutritionList,
        allergy.StudentAllergies
    FROM MealPackages mp
    INNER JOIN MealPackageWeeks mpw
        ON mpw.MealPackageId = mp.Id AND mpw.WeekNo = @WeekNo
    INNER JOIN MealPackageDays mpd
        ON mpd.MealPackageId = mp.Id AND mpd.DayId = @DayId
    LEFT JOIN Enums mt ON mp.MealTypeId = mt.Id
    LEFT JOIN Enums mc ON mp.MealCategotyId = mc.Id
    OUTER APPLY (
        SELECT STRING_AGG(mi.ItemName, ', ') AS ItemsName
        FROM MealPackageItems mpi
        INNER JOIN MealItem mi ON mpi.MealItemId = mi.Id
        WHERE mpi.MealPackageId = mp.Id
    ) items
    OUTER APPLY (
        SELECT STRING_AGG(CAST(w.WeekNo AS VARCHAR(10)), ', ') AS WeekNo
        FROM MealPackageWeeks w
        WHERE w.MealPackageId = mp.Id
    ) weeks
    OUTER APPLY (
        SELECT STRING_AGG(CAST(mii.IngredientId AS VARCHAR(10)), ', ') AS IngredientIds
        FROM (
            SELECT DISTINCT mii.IngredientId
            FROM MealPackageItems mpi
            INNER JOIN MealItem mi ON mpi.MealItemId = mi.Id
            INNER JOIN MealItemIngredients mii ON mi.Id = mii.MealItemId
            WHERE mpi.MealPackageId = mp.Id
        ) mii
    ) ing
    OUTER APPLY (
        SELECT (
            SELECT
                MIN(min_row.Id) AS Id,
                min_row.NutritionId,
                ISNULL(n.EnumValue, '') AS NutritionName,
                ISNULL(measure.EnumValue, '') AS MeasureTypeName,
                SUM(min_row.MeasureValue) AS MeasureValue,
                ISNULL(n.ClassName, '') AS ClassName
            FROM MealPackageItems mpi
            INNER JOIN MealItem mi ON mpi.MealItemId = mi.Id
            INNER JOIN MealItemNutrition min_row ON mi.Id = min_row.MealItemId
            LEFT JOIN Enums n ON min_row.NutritionId = n.Id
            LEFT JOIN Enums measure ON min_row.MeasureTypeId = measure.Id
            WHERE mpi.MealPackageId = mp.Id
            GROUP BY min_row.NutritionId, n.EnumValue, measure.EnumValue, n.ClassName
            FOR JSON PATH
        ) AS NutritionList
    ) nutr
    OUTER APPLY (
        SELECT (
            SELECT
                mii.Id,
                mii.IngredientId AS AllergyItemId,
                i.EnumValue AS AllergyItemName
            FROM MealPackageItems mpi
            INNER JOIN MealItem mi ON mpi.MealItemId = mi.Id
            INNER JOIN MealItemIngredients mii ON mi.Id = mii.MealItemId
            INNER JOIN Enums i ON mii.IngredientId = i.Id
            WHERE mpi.MealPackageId = mp.Id
                AND mii.IngredientId IN (SELECT AllergyItemId FROM @StudentAllergies)
            FOR JSON PATH
        ) AS StudentAllergies
    ) allergy
    WHERE mp.IsActive = 1
        AND (mp.IsDeleted IS NULL OR mp.IsDeleted = 0)
        AND mp.SchoolId = @SchoolId
        AND (@MealTypeId IS NULL OR mp.MealTypeId = @MealTypeId)
        AND (
            NOT EXISTS (
                SELECT 1
                FROM MealPackageServingPeriod sp0
                WHERE sp0.SchoolId = mp.SchoolId
            )
            OR EXISTS (
                SELECT 1
                FROM MealPackageServingPeriod sp
                WHERE sp.SchoolId = mp.SchoolId
                    AND (sp.StartDate IS NULL OR CAST(GETDATE() AS DATE) >= CAST(sp.StartDate AS DATE))
                    AND (sp.CutoffDate IS NULL OR CAST(GETDATE() AS DATE) <= CAST(sp.CutoffDate AS DATE))
            )
        );
END
GO
