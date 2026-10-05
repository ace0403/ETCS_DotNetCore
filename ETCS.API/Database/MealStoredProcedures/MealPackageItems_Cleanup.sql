-- Optional cleanup after Meal Combo admin no longer links meal items.
-- Review row counts before running in production.

USE MealDB;
GO

-- Preview orphaned links (combos no longer maintained via MealPackageItems in admin).
SELECT COUNT(*) AS MealPackageItemLinks
FROM MealPackageItems;
GO

-- Uncomment to remove all MealPackageItems rows:
-- DELETE FROM MealPackageItems;
-- GO
