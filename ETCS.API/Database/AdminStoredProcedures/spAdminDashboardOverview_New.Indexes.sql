/*
Optional indexes for spAdminDashboardOverview_New on ibonus.
IX_AccessLog_CanteenReport from spCanteentranSummary_New.Indexes.sql also benefits this SP.
*/
SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_AccessLog_DashboardTerminal'
      AND object_id = OBJECT_ID(N'dbo.AccessLog')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_AccessLog_DashboardTerminal
        ON dbo.AccessLog (LogDateTimeTerminal, BranchCode, TransactionType)
        INCLUDE (CustomerID, Amount, TerminalCode, TransactionID);
END
GO
