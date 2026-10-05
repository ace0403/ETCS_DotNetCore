/*
Deploy on MealDB database.
School weekly schedule + date-based calendar exceptions (holidays / half-days).

Do NOT run from automated tooling — execute manually when ready.
*/
USE MealDB;
GO

IF OBJECT_ID(N'dbo.SchoolWeeklySchedule', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchoolWeeklySchedule
    (
        SchoolId  INT     NOT NULL,
        DayOfWeek TINYINT NOT NULL, -- 0=Sunday … 6=Saturday (.NET DayOfWeek)
        DayStatus TINYINT NOT NULL, -- 0=Holiday, 1=FullDay, 2=HalfDay
        CONSTRAINT PK_SchoolWeeklySchedule PRIMARY KEY (SchoolId, DayOfWeek),
        CONSTRAINT CK_SchoolWeeklySchedule_DayOfWeek CHECK (DayOfWeek BETWEEN 0 AND 6),
        CONSTRAINT CK_SchoolWeeklySchedule_DayStatus CHECK (DayStatus IN (0, 1, 2))
    );

    CREATE INDEX IX_SchoolWeeklySchedule_SchoolId
        ON dbo.SchoolWeeklySchedule (SchoolId)
        INCLUDE (DayOfWeek, DayStatus);
END
GO

IF OBJECT_ID(N'dbo.SchoolCalendarExceptions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchoolCalendarExceptions
    (
        Id             INT            NOT NULL IDENTITY(1, 1),
        SchoolId       INT            NOT NULL,
        ExceptionDate  DATE           NOT NULL,
        DayStatus      TINYINT        NOT NULL, -- 0=Holiday, 1=FullDay, 2=HalfDay
        Title          NVARCHAR(100)  NOT NULL CONSTRAINT DF_SchoolCalendarExceptions_Title DEFAULT (N''),
        Notes          NVARCHAR(500)  NULL,
        CreatedOn      DATETIME       NOT NULL CONSTRAINT DF_SchoolCalendarExceptions_CreatedOn DEFAULT (GETDATE()),
        CONSTRAINT PK_SchoolCalendarExceptions PRIMARY KEY (Id),
        CONSTRAINT UQ_SchoolCalendarExceptions_SchoolDate UNIQUE (SchoolId, ExceptionDate),
        CONSTRAINT CK_SchoolCalendarExceptions_DayStatus CHECK (DayStatus IN (0, 1, 2))
    );

    CREATE INDEX IX_SchoolCalendarExceptions_School_Date
        ON dbo.SchoolCalendarExceptions (SchoolId, ExceptionDate)
        INCLUDE (DayStatus, Title);
END
GO

-- Default weekly template for all schools in main DB:
-- Mon–Thu = FullDay (1), Fri = HalfDay (2), Sat–Sun = Holiday (0)
;WITH SchoolIds AS
(
    SELECT DISTINCT SchoolId
    FROM ibonus.dbo.SchoolInfo
    WHERE SchoolId IS NOT NULL
),
Defaults AS
(
    SELECT DayOfWeek, DayStatus
    FROM (VALUES
        (0, 0), -- Sunday Holiday
        (1, 1), -- Monday FullDay
        (2, 1), -- Tuesday FullDay
        (3, 1), -- Wednesday FullDay
        (4, 1), -- Thursday FullDay
        (5, 2), -- Friday HalfDay
        (6, 0)  -- Saturday Holiday
    ) AS d (DayOfWeek, DayStatus)
)
INSERT INTO dbo.SchoolWeeklySchedule (SchoolId, DayOfWeek, DayStatus)
SELECT s.SchoolId, d.DayOfWeek, d.DayStatus
FROM SchoolIds s
CROSS JOIN Defaults d
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.SchoolWeeklySchedule w
    WHERE w.SchoolId = s.SchoolId
      AND w.DayOfWeek = d.DayOfWeek
);
GO
