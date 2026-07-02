/*
Deploy on MealDB database.
Serving period order windows for meal package combo ordering.
*/
USE MealDB;
GO

IF OBJECT_ID(N'dbo.MealPackageServingPeriod', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MealPackageServingPeriod
    (
        Id INT IDENTITY(1, 1) NOT NULL CONSTRAINT PK_MealPackageServingPeriod PRIMARY KEY,
        SchoolId INT NOT NULL,
        StartDate DATETIME NULL,
        CutoffDate DATETIME NULL
    );

    CREATE INDEX IX_MealPackageServingPeriod_SchoolId
        ON dbo.MealPackageServingPeriod (SchoolId);
END
GO
