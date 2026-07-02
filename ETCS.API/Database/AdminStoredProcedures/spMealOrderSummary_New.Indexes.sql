/*
Optional indexes for spMealOrderSummary_New on ibonus.
Review on a non-production window first.
*/
SET NOCOUNT ON;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_MealOrders_DeliveryDate_Paid'
      AND object_id = OBJECT_ID(N'dbo.MealOrders')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_MealOrders_DeliveryDate_Paid
        ON dbo.MealOrders (DeliveryDate, PaymentStatus)
        INCLUDE (MealID, StudCode, [Day], [Week], OrderDate, OrderID);
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = N'IX_StudentLogin_StudCode_School'
      AND object_id = OBJECT_ID(N'dbo.StudentLogin')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_StudentLogin_StudCode_School
        ON dbo.StudentLogin (StudCode, StudSchoolId)
        INCLUDE (StudFirstName, StudLastName, StudStd, StudDiv);
END
GO
