-- Optimized: pre-filter by week/day, package-level ingredients/nutrition, optional meal session/type.
-- Run against MealDB after MealMenuPerformanceIndexes.sql and MealItem_MealSessionId.sql.

USE MealDB;
GO

CREATE OR ALTER PROCEDURE [dbo].[GetMealPackagesForStudent]
    @StudentId INT,
    @SchoolId INT,
    @WeekNo INT,
    @DayId INT,
    @MealDate DATETIME,
    @MealSessionId INT = NULL,
    @MealTypeId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @StudentAllergies TABLE (AllergyItemId INT PRIMARY KEY);
    INSERT INTO @StudentAllergies (AllergyItemId)
    SELECT DISTINCT AllergyItemId
    FROM StudentAllergies
    WHERE StudentId = @StudentId;

    SELECT
        mp.Id,
        mp.PackageName,
        mp.MealSessionId,
        ISNULL(ms.EnumValue, '') AS MealSessionName,
        ISNULL(ms.ClassName, '') AS MealSessionCssClass,
        mp.MealTypeId,
        ISNULL(mt.EnumValue, '') AS MealTypeName,
        ISNULL(mt.ClassName, '') AS MealCssClass,
        ISNULL(mt.SortOrder, 2147483647) AS MealTypeSortOrder,
        mp.MealCategotyId AS MealCategoryId,
        ISNULL(mc.EnumValue, '') AS MealCategoryName,
        mp.SchoolId,
        mp.ImageName,
        ISNULL(mp.Detail, '') AS Detail,
        mp.Price,
        ISNULL(mp.ProcessingFee, 0) AS ProcessingFee,
        @MealDate AS CreatedOn,
        ISNULL(NULLIF(LTRIM(RTRIM(mp.Detail)), ''), '') AS ItemsName,
        weeks.WeekNo,
        ing.IngredientIds,
        ingNames.Ingredients,
        nutr.NutritionList,
        allergy.StudentAllergies
    FROM MealPackages mp
    INNER JOIN MealPackageWeeks mpw
        ON mpw.MealPackageId = mp.Id AND mpw.WeekNo = @WeekNo
    INNER JOIN MealPackageDays mpd
        ON mpd.MealPackageId = mp.Id AND mpd.DayId = @DayId
    LEFT JOIN Enums ms ON mp.MealSessionId = ms.Id
    LEFT JOIN Enums mt ON mp.MealTypeId = mt.Id
    LEFT JOIN Enums mc ON mp.MealCategotyId = mc.Id
    OUTER APPLY (
        SELECT STRING_AGG(CAST(w.WeekNo AS VARCHAR(10)), ', ') AS WeekNo
        FROM MealPackageWeeks w
        WHERE w.MealPackageId = mp.Id
    ) weeks
    OUTER APPLY (
        SELECT STRING_AGG(CAST(mpi.IngredientId AS VARCHAR(10)), ', ') AS IngredientIds
        FROM (
            SELECT DISTINCT mpi.IngredientId
            FROM MealPackageIngredients mpi
            WHERE mpi.MealPackageId = mp.Id
        ) mpi
    ) ing
    OUTER APPLY (
        SELECT (
            SELECT src.Name, src.Icon
            FROM (
                SELECT DISTINCT
                    LTRIM(RTRIM(ISNULL(i.EnumValue, ''))) AS Name,
                    LTRIM(RTRIM(ISNULL(i.Icon, ''))) AS Icon
                FROM MealPackageIngredients mpi
                INNER JOIN Enums i ON mpi.IngredientId = i.Id
                WHERE mpi.MealPackageId = mp.Id
            ) src
            WHERE src.Name <> ''
            FOR JSON PATH
        ) AS Ingredients
    ) ingNames
    OUTER APPLY (
        SELECT (
            SELECT
                pn.Id,
                pn.NutritionId,
                ISNULL(n.EnumValue, '') AS NutritionName,
                ISNULL(measure.EnumValue, '') AS MeasureTypeName,
                pn.MeasureValue,
                ISNULL(n.ClassName, '') AS ClassName
            FROM MealPackageNutrition pn
            LEFT JOIN Enums n ON pn.NutritionId = n.Id
            LEFT JOIN Enums measure ON pn.MeasureTypeId = measure.Id
            WHERE pn.MealPackageId = mp.Id
            FOR JSON PATH
        ) AS NutritionList
    ) nutr
    OUTER APPLY (
        SELECT (
            SELECT
                mpi.Id,
                mpi.IngredientId AS AllergyItemId,
                i.EnumValue AS AllergyItemName
            FROM MealPackageIngredients mpi
            INNER JOIN Enums i ON mpi.IngredientId = i.Id
            WHERE mpi.MealPackageId = mp.Id
                AND mpi.IngredientId IN (SELECT AllergyItemId FROM @StudentAllergies)
            FOR JSON PATH
        ) AS StudentAllergies
    ) allergy
    WHERE mp.IsActive = 1
        AND (mp.IsDeleted IS NULL OR mp.IsDeleted = 0)
        AND mp.SchoolId = @SchoolId
        AND ISNULL(ms.IsActive, 1) = 1
        AND (@MealSessionId IS NULL OR mp.MealSessionId = @MealSessionId)
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
