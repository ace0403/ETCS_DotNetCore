/*
Deploy on MealDB database.
Junction of student (ibonus StudentLogin.UserId) to student-selectable transaction types
(Enums.Id 23, 24, 42; MealEnumTypeIds.StudentTransactionType = 7).

Do NOT run from automated tooling — execute manually when ready.
*/
USE MealDB;
GO

IF OBJECT_ID(N'dbo.StudentOrderTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StudentOrderTypes
    (
        StudentId   DECIMAL(18, 0) NOT NULL,
        OrderTypeId INT            NOT NULL,
        CreatedOn   DATETIME       NOT NULL CONSTRAINT DF_StudentOrderTypes_CreatedOn DEFAULT (GETDATE()),
        CONSTRAINT PK_StudentOrderTypes PRIMARY KEY (StudentId, OrderTypeId)
    );

    CREATE INDEX IX_StudentOrderTypes_StudentId
        ON dbo.StudentOrderTypes (StudentId)
        INCLUDE (OrderTypeId);
END
GO

