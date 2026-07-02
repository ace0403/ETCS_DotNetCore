/*
Optional indexes for spMealOrderSummary_MealDB_New on MealDB.
Run after the procedure is deployed.
*/
USE MealDB;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_OrderItem_MealDate_OrderId'
      AND object_id = OBJECT_ID(N'dbo.OrderItem')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_OrderItem_MealDate_OrderId
        ON dbo.OrderItem (MealDate, OrderId)
        INCLUDE (PackageId, ItemId);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_Order_IsPaid_OrderTypeId'
      AND object_id = OBJECT_ID(N'dbo.[Order]')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Order_IsPaid_OrderTypeId
        ON dbo.[Order] (IsPaid, OrderTypeId)
        INCLUDE (OrderDate, StudentId);
END
GO
