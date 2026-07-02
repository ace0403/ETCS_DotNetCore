/*
Optional indexes for spCanteentranSummary_New on ibonus.

Review on a non-production window first. Adjust ONLINE = ON if your edition supports it.
Existing indexes with overlapping keys can make some of these redundant — check sys.indexes first.
*/
SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_AccessLog_CanteenReport'
      AND object_id = OBJECT_ID(N'dbo.AccessLog')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_AccessLog_CanteenReport
        ON dbo.AccessLog (LogDateTimeTerminal, TransactionType)
        INCLUDE (
            CustomerID,
            TerminalCode,
            BranchCode,
            TransactionID,
            Amount,
            BalPrepaid,
            CardID,
            Description
        );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_POSPurchase_TransId_CustomerId'
      AND object_id = OBJECT_ID(N'dbo.POSPurchase')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_POSPurchase_TransId_CustomerId
        ON dbo.POSPurchase (TransId, Customerid)
        INCLUDE (SkuCode, Amount, Id);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_IDTerminals_TerminalCode_BranchCode'
      AND object_id = OBJECT_ID(N'dbo.IDTerminals')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_IDTerminals_TerminalCode_BranchCode
        ON dbo.IDTerminals (TerminalCode, branchcode)
        INCLUDE (Description);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_StudentLogin_CustomerID'
      AND object_id = OBJECT_ID(N'dbo.StudentLogin')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_StudentLogin_CustomerID
        ON dbo.StudentLogin (CustomerID)
        INCLUDE (StudFirstName, StudLastName);
END
GO
