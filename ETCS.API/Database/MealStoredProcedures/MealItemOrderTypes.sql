
USE MealDB;
GO

IF OBJECT_ID(N'dbo.MealItemOrderTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MealItemOrderTypes
    (
        MealItemId  INT      NOT NULL,
        OrderTypeId INT      NOT NULL,
        CreatedOn   DATETIME NOT NULL CONSTRAINT DF_MealItemOrderTypes_CreatedOn DEFAULT (GETDATE()),
        CONSTRAINT PK_MealItemOrderTypes PRIMARY KEY (MealItemId, OrderTypeId)
    );

    CREATE INDEX IX_MealItemOrderTypes_MealItemId
        ON dbo.MealItemOrderTypes (MealItemId)
        INCLUDE (OrderTypeId);
END
GO
