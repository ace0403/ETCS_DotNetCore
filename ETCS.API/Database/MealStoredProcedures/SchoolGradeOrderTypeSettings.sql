/*
Deploy on MealDB database.
Per-school, per-grade order-type settings (no-service flag).

Do NOT run from automated tooling — execute manually when ready.
*/
USE MealDB;
GO

IF OBJECT_ID(N'dbo.SchoolGradeOrderTypeSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchoolGradeOrderTypeSettings
    (
        SchoolId     INT      NOT NULL,
        GradeId      INT      NOT NULL,
        IsNoService  BIT      NOT NULL CONSTRAINT DF_SchoolGradeOrderTypeSettings_IsNoService DEFAULT (0),
        CreatedOn    DATETIME NOT NULL CONSTRAINT DF_SchoolGradeOrderTypeSettings_CreatedOn DEFAULT (GETDATE()),
        CONSTRAINT PK_SchoolGradeOrderTypeSettings PRIMARY KEY (SchoolId, GradeId)
    );

    CREATE INDEX IX_SchoolGradeOrderTypeSettings_SchoolId
        ON dbo.SchoolGradeOrderTypeSettings (SchoolId)
        INCLUDE (GradeId, IsNoService);
END
GO
