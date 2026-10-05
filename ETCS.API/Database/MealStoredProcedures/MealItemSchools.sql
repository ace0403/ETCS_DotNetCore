/*
Deploy on MealDB database.
Junction of meal item to schools (one item can be linked to many schools).

Includes backfill from MealItem.SchoolId for existing rows.

Do NOT run from automated tooling — execute manually when ready.
*/
USE MealDB;
GO

IF OBJECT_ID(N'dbo.MealItemSchools', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MealItemSchools
    (
        MealItemId INT      NOT NULL,
        SchoolId   INT      NOT NULL,
        CreatedOn  DATETIME NOT NULL CONSTRAINT DF_MealItemSchools_CreatedOn DEFAULT (GETDATE()),
        CONSTRAINT PK_MealItemSchools PRIMARY KEY (MealItemId, SchoolId)
    );

    CREATE INDEX IX_MealItemSchools_SchoolId
        ON dbo.MealItemSchools (SchoolId)
        INCLUDE (MealItemId);
END
GO

INSERT INTO dbo.MealItemSchools (MealItemId, SchoolId, CreatedOn)
SELECT mi.Id, mi.SchoolId, GETDATE()
FROM dbo.MealItem mi
WHERE mi.SchoolId IS NOT NULL
  AND ISNULL(mi.IsDeleted, 0) = 0
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.MealItemSchools mis
      WHERE mis.MealItemId = mi.Id
        AND mis.SchoolId = mi.SchoolId
  );
GO
