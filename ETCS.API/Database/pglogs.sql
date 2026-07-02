-- Run against the ibonus database (Database:ConnectionString).
IF NOT EXISTS (
    SELECT 1
    FROM sys.tables
    WHERE name = N'PGLogs' AND schema_id = SCHEMA_ID(N'dbo'))
BEGIN
    CREATE TABLE dbo.PGLogs
    (
        Id INT IDENTITY(1, 1) NOT NULL PRIMARY KEY,
        TransactionId NVARCHAR(100) NOT NULL,
        Result NVARCHAR(MAX) NULL,
        [Date] DATETIME NOT NULL CONSTRAINT DF_PGLogs_Date DEFAULT (GETDATE())
    );
END;
