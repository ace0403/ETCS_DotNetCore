-- Add MealSessionId to MealItem and MealPackages.
-- Meal sessions are parent Enums rows (EnumTypeId = 9, ParentId IS NULL).
-- Meal types are child Enums rows (EnumTypeId = 9, ParentId = session Id).

IF COL_LENGTH('MealItem', 'MealSessionId') IS NULL
BEGIN
    ALTER TABLE MealItem ADD MealSessionId INT NULL;
END
GO

IF COL_LENGTH('MealPackages', 'MealSessionId') IS NULL
BEGIN
    ALTER TABLE MealPackages ADD MealSessionId INT NULL;
END
GO

-- Backfill from existing MealTypeId parent enum.
UPDATE mi
SET MealSessionId = e.ParentId
FROM MealItem mi
INNER JOIN Enums e ON e.Id = mi.MealTypeId
WHERE mi.MealSessionId IS NULL
  AND e.ParentId IS NOT NULL;
GO

UPDATE mp
SET MealSessionId = e.ParentId
FROM MealPackages mp
INNER JOIN Enums e ON e.Id = mp.MealTypeId
WHERE mp.MealSessionId IS NULL
  AND e.ParentId IS NOT NULL;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_MealItem_Session_Type'
      AND object_id = OBJECT_ID('MealItem')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_MealItem_Session_Type
        ON MealItem (MealSessionId, MealTypeId)
        WHERE ISNULL(IsDeleted, 0) = 0;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_MealPackages_Session_Type'
      AND object_id = OBJECT_ID('MealPackages')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_MealPackages_Session_Type
        ON MealPackages (MealSessionId, MealTypeId)
        WHERE ISNULL(IsDeleted, 0) = 0;
END
GO
