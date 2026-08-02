-- Meal menu performance indexes for GetMealItemsForStudent / GetMealPackagesForStudent.
-- Run against MealDB.

USE MealDB;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_MealItemWeeks_WeekNo_MealItemId' AND object_id = OBJECT_ID('MealItemWeeks'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MealItemWeeks_WeekNo_MealItemId
        ON MealItemWeeks (WeekNo, MealItemId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_MealItemDays_DayId_MealItemId' AND object_id = OBJECT_ID('MealItemDays'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MealItemDays_DayId_MealItemId
        ON MealItemDays (DayId, MealItemId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_MealPackageWeeks_WeekNo_MealPackageId' AND object_id = OBJECT_ID('MealPackageWeeks'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MealPackageWeeks_WeekNo_MealPackageId
        ON MealPackageWeeks (WeekNo, MealPackageId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_MealPackageDays_DayId_MealPackageId' AND object_id = OBJECT_ID('MealPackageDays'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MealPackageDays_DayId_MealPackageId
        ON MealPackageDays (DayId, MealPackageId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_MealItemIngredients_MealItemId' AND object_id = OBJECT_ID('MealItemIngredients'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MealItemIngredients_MealItemId
        ON MealItemIngredients (MealItemId)
        INCLUDE (IngredientId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_MealItemNutrition_MealItemId' AND object_id = OBJECT_ID('MealItemNutrition'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MealItemNutrition_MealItemId
        ON MealItemNutrition (MealItemId)
        INCLUDE (NutritionId, MeasureTypeId, MeasureValue);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_MealPackageItems_MealPackageId' AND object_id = OBJECT_ID('MealPackageItems'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MealPackageItems_MealPackageId
        ON MealPackageItems (MealPackageId)
        INCLUDE (MealItemId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_StudentAllergies_StudentId' AND object_id = OBJECT_ID('StudentAllergies'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_StudentAllergies_StudentId
        ON StudentAllergies (StudentId)
        INCLUDE (AllergyItemId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_MealItem_Active_Type' AND object_id = OBJECT_ID('MealItem'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MealItem_Active_Type
        ON MealItem (IsActive, MealTypeId, IsDeleted)
        INCLUDE (SchoolId, ItemName, Detail, Price, ImageName, MealCategotyId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_MealPackages_Active_Type' AND object_id = OBJECT_ID('MealPackages'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_MealPackages_Active_Type
        ON MealPackages (IsActive, MealTypeId, IsDeleted)
        INCLUDE (SchoolId, PackageName, Detail, Price, ImageName, MealCategotyId);
END
GO
