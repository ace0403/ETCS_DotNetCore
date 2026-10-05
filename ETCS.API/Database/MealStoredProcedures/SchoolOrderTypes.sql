/*
Deploy on MealDB database.
Junction of school to student-selectable transaction types (Enums.Id 23, 24, 42).

Do NOT run from automated tooling — execute manually when ready.
*/
USE MealDB;
GO

IF OBJECT_ID(N'dbo.SchoolOrderTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchoolOrderTypes
    (
        SchoolId    INT      NOT NULL,
        OrderTypeId INT      NOT NULL,
        CreatedOn   DATETIME NOT NULL CONSTRAINT DF_SchoolOrderTypes_CreatedOn DEFAULT (GETDATE()),
        CONSTRAINT PK_SchoolOrderTypes PRIMARY KEY (SchoolId, OrderTypeId)
    );

    CREATE INDEX IX_SchoolOrderTypes_SchoolId
        ON dbo.SchoolOrderTypes (SchoolId)
        INCLUDE (OrderTypeId);
END
GO
