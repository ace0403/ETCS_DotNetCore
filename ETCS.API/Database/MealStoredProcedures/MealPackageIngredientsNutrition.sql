/*
Deploy on MealDB database.
Package-level ingredients and nutrition for meal combos.
Do NOT run from automated tooling — execute manually when ready.
*/
USE MealDB;
GO

IF OBJECT_ID(N'dbo.MealPackageIngredients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MealPackageIngredients
    (
        Id            INT      IDENTITY(1, 1) NOT NULL
            CONSTRAINT PK_MealPackageIngredients PRIMARY KEY,
        MealPackageId INT      NOT NULL,
        IngredientId  INT      NOT NULL,
        CreatedOn     DATETIME NOT NULL
            CONSTRAINT DF_MealPackageIngredients_CreatedOn DEFAULT (GETUTCDATE())
    );

    CREATE NONCLUSTERED INDEX IX_MealPackageIngredients_MealPackageId
        ON dbo.MealPackageIngredients (MealPackageId)
        INCLUDE (IngredientId);
END
GO

IF OBJECT_ID(N'dbo.MealPackageNutrition', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MealPackageNutrition
    (
        Id            INT            IDENTITY(1, 1) NOT NULL
            CONSTRAINT PK_MealPackageNutrition PRIMARY KEY,
        MealPackageId INT            NOT NULL,
        NutritionId   INT            NOT NULL,
        MeasureValue  DECIMAL(18, 2) NOT NULL,
        MeasureTypeId INT            NOT NULL,
        CreatedBy     INT            NULL,
        CreatedOn     DATETIME       NOT NULL
            CONSTRAINT DF_MealPackageNutrition_CreatedOn DEFAULT (GETUTCDATE())
    );

    CREATE NONCLUSTERED INDEX IX_MealPackageNutrition_MealPackageId
        ON dbo.MealPackageNutrition (MealPackageId)
        INCLUDE (NutritionId, MeasureTypeId, MeasureValue);
END
GO
