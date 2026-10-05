/*
Deploy on MealDB database.
Junction of school+grade to student-selectable transaction types (Enums.Id 23, 24).

Do NOT run from automated tooling — execute manually when ready.
*/
USE MealDB;
GO

IF OBJECT_ID(N'dbo.SchoolGradeOrderTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchoolGradeOrderTypes
    (
        SchoolId    INT      NOT NULL,
        GradeId     INT      NOT NULL,
        OrderTypeId INT      NOT NULL,
        CreatedOn   DATETIME NOT NULL CONSTRAINT DF_SchoolGradeOrderTypes_CreatedOn DEFAULT (GETDATE()),
        CONSTRAINT PK_SchoolGradeOrderTypes PRIMARY KEY (SchoolId, GradeId, OrderTypeId)
    );

    CREATE INDEX IX_SchoolGradeOrderTypes_SchoolGrade
        ON dbo.SchoolGradeOrderTypes (SchoolId, GradeId)
        INCLUDE (OrderTypeId);
END
GO
