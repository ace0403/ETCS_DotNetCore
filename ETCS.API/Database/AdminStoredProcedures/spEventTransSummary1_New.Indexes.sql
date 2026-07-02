/*
Optional indexes for spEventTransSummary1_New on ibonus.

Review on a non-production window first. IX_IDTerminals_TerminalCode_BranchCode
from spCanteentranSummary_New.Indexes.sql also benefits this report.
*/
SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_AccessLog_TerminalSalesSummary'
      AND object_id = OBJECT_ID(N'dbo.AccessLog')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_AccessLog_TerminalSalesSummary
        ON dbo.AccessLog (LogDateTimeTerminal, TransactionType)
        INCLUDE (
            CustomerID,
            TerminalCode,
            BranchCode,
            Amount
        );
END
GO
