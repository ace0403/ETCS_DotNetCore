-- Optimized: pre-filter by week/day, optional meal type, indexed joins.
-- Run against MealDB after MealMenuPerformanceIndexes.sql.

USE MealDB;
GO

CREATE OR ALTER PROCEDURE [dbo].[GetMealItemsForStudent]
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
        mi.Id,
        mi.ItemName,
        mi.MealTypeId,
        ISNULL(mt.EnumValue, '') AS MealTypeName,
        ISNULL(mt.ClassName, '') AS MealCssClass,
        mi.MealCategotyId AS MealCategoryId,
        ISNULL(mc.EnumValue, '') AS MealCategoryName,
        mi.SchoolId,
        mi.ImageName,
        ISNULL(mi.Detail, '') AS Detail,
        mi.Price,
        @MealDate AS CreatedOn,
        ing.IngredientIds,
        nutr.NutritionList,
        allergy.StudentAllergies
    FROM MealItem mi
    INNER JOIN MealItemWeeks miw
        ON miw.MealItemId = mi.Id AND miw.WeekNo = @WeekNo
    INNER JOIN MealItemDays mid
        ON mid.MealItemId = mi.Id AND mid.DayId = @DayId
    LEFT JOIN Enums mt ON mi.MealTypeId = mt.Id
    LEFT JOIN Enums mc ON mi.MealCategotyId = mc.Id
    OUTER APPLY (
        SELECT STRING_AGG(CAST(mii.IngredientId AS VARCHAR(10)), ', ') AS IngredientIds
        FROM (
            SELECT DISTINCT mii.IngredientId
            FROM MealItemIngredients mii
            WHERE mii.MealItemId = mi.Id
        ) mii
    ) ing
    OUTER APPLY (
        SELECT (
            SELECT
                min_row.Id,
                min_row.NutritionId,
                ISNULL(n.EnumValue, '') AS NutritionName,
                ISNULL(measure.EnumValue, '') AS MeasureTypeName,
                min_row.MeasureValue,
                ISNULL(n.ClassName, '') AS ClassName
            FROM MealItemNutrition min_row
            LEFT JOIN Enums n ON min_row.NutritionId = n.Id
            LEFT JOIN Enums measure ON min_row.MeasureTypeId = measure.Id
            WHERE min_row.MealItemId = mi.Id
            FOR JSON PATH
        ) AS NutritionList
    ) nutr
    OUTER APPLY (
        SELECT (
            SELECT
                mii.MealItemId AS Id,
                mii.IngredientId AS AllergyItemId,
                i.EnumValue AS AllergyItemName
            FROM MealItemIngredients mii
            INNER JOIN Enums i ON mii.IngredientId = i.Id
            WHERE mii.MealItemId = mi.Id
                AND mii.IngredientId IN (SELECT AllergyItemId FROM @StudentAllergies)
            FOR JSON PATH
        ) AS StudentAllergies
    ) allergy
    WHERE mi.IsActive = 1
        AND (mi.IsDeleted IS NULL OR mi.IsDeleted = 0)
        AND mi.SchoolId = @SchoolId
        AND (@MealTypeId IS NULL OR mi.MealTypeId = @MealTypeId);
END
GO
