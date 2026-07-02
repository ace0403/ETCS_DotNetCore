-- Optional unified spend-limit stored procedure for POS.
-- Execute manually in ibonus. Mirrors legacy spGetSpendLimitInfo used by old AVISoap POS.

/*
CREATE OR ALTER PROCEDURE dbo.spPosGetSpendLimitInfo
    @CustomerId NVARCHAR(16),
    @CurrentDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET @CurrentDate = ISNULL(@CurrentDate, CAST(GETDATE() AS date));

    SELECT
        DailySpent = CAST(0 AS decimal(18,2)),
        DailyUndo = CAST(0 AS decimal(18,2)),
        WeeklySpent = CAST(0 AS decimal(18,2)),
        WeeklyUndo = CAST(0 AS decimal(18,2)),
        DailyLimit = CAST(ISNULL(sl.DailyLimit, 0) AS decimal(18,2)),
        WeeklyLimit = CAST(ISNULL(sl.WeeklyLimit, 0) AS decimal(18,2))
    FROM StudentLogin sl
    WHERE LTRIM(RTRIM(sl.CustomerId)) = LTRIM(RTRIM(@CustomerId));
END
*/
