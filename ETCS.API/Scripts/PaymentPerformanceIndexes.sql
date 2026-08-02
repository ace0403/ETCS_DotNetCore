-- Recommended indexes for ETCS payment and order performance.
-- Run against MealDB and ibonus as noted.

-- MealDB
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Order_OrderId' AND object_id = OBJECT_ID('[Order]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Order_OrderId ON [Order] (OrderId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Transaction_Remarks' AND object_id = OBJECT_ID('[Transaction]'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Transaction_Remarks ON [Transaction] (Remarks)
    INCLUDE (TransactionId, IsTransactionCompleted, StatusId);
END
GO

-- ibonus (main DB)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PendingTransInfo_Remarks' AND object_id = OBJECT_ID('PendingTransInfo'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_PendingTransInfo_Remarks ON PendingTransInfo (Remarks);
END
GO
