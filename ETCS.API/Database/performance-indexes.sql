-- Performance indexes for payment and order flows (run on MealDB and ibonus as noted).

-- ibonus
USE ibonus;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PendingTransInfo_Remarks' AND object_id = OBJECT_ID('PendingTransInfo'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_PendingTransInfo_Remarks
        ON PendingTransInfo (Remarks);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PGLogs_TransactionId' AND object_id = OBJECT_ID('PGLogs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_PGLogs_TransactionId
        ON PGLogs (TransactionId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_PGLogs_Date' AND object_id = OBJECT_ID('PGLogs'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_PGLogs_Date
        ON PGLogs ([Date]);
END
GO

-- MealDB
USE MealDB;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Transaction_Remarks' AND object_id = OBJECT_ID('[Transaction]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Transaction_Remarks
        ON [Transaction] (Remarks);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Transaction_GuardianId_StudentId' AND object_id = OBJECT_ID('[Transaction]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Transaction_GuardianId_StudentId
        ON [Transaction] (GuardianId, StudentId)
        INCLUDE (CreatedOn);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UQ_Order_OrderId' AND object_id = OBJECT_ID('[Order]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_Order_OrderId
        ON [Order] (OrderId);
END
GO
