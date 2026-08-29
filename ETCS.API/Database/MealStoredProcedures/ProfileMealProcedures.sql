-- Profile meal stored procedures (run in SSMS against MealDB).
-- Replace sample parameters with real student/week/day values from production.

USE MealDB;
GO

SET STATISTICS IO, TIME ON;
GO

EXEC dbo.GetMealItemsForStudent
    @StudentId = 1,
    @SchoolId = 1,
    @WeekNo = 4,
    @DayId = 4,
    @MealDate = '2026-05-28',
    @MealSessionId = NULL,
    @MealTypeId = NULL;
GO

EXEC dbo.GetMealPackagesForStudent
    @StudentId = 1,
    @SchoolId = 1,
    @WeekNo = 4,
    @DayId = 4,
    @MealDate = '2026-05-28',
    @MealSessionId = NULL,
    @MealTypeId = NULL;
GO

SET STATISTICS IO, TIME OFF;
GO
