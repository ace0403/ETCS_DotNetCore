/*
Optional indexes for spRptAdmineTransaction_New on ibonus.
Review existing indexes before deploying.
*/
SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_AccessLog_AdminTxnReport'
      AND object_id = OBJECT_ID(N'dbo.AccessLog')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_AccessLog_AdminTxnReport
        ON dbo.AccessLog (LogDateTimeServer, TransactionType)
        INCLUDE (
            LogDateTimeTerminal,
            CustomerID,
            Amount,
            TerminalCode,
            BranchCode,
            TransactionID
        );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_AccessLog_AdminTxnReport_Terminal'
      AND object_id = OBJECT_ID(N'dbo.AccessLog')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_AccessLog_AdminTxnReport_Terminal
        ON dbo.AccessLog (LogDateTimeTerminal, TransactionType, CustomerID)
        INCLUDE (
            Amount,
            TerminalCode,
            BranchCode,
            TransactionID
        );
END
GO
