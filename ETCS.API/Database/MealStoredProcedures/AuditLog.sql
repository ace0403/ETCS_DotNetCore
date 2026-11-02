/*
Deploy on MealDB.
Append-only application audit log. The application never updates or deletes these rows.

Do NOT run from automated tooling — execute manually when ready.
*/
USE MealDB;
GO

IF OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AuditLog
    (
        AuditLogId     BIGINT           IDENTITY(1, 1) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
        OccurredOnUtc  DATETIME2(3)     NOT NULL CONSTRAINT DF_AuditLog_OccurredOnUtc DEFAULT (SYSUTCDATETIME()),
        CorrelationId  UNIQUEIDENTIFIER NOT NULL,
        SourceApp      NVARCHAR(32)     NOT NULL,
        ActorType      NVARCHAR(32)     NOT NULL,
        ActorId        INT              NULL,
        ActorName      NVARCHAR(256)    NULL,
        Action         NVARCHAR(64)     NOT NULL,
        EntityType     NVARCHAR(128)    NOT NULL,
        EntityId       NVARCHAR(64)     NULL,
        Outcome        NVARCHAR(32)     NOT NULL,
        Message        NVARCHAR(1000)   NULL,
        HttpMethod     NVARCHAR(16)     NULL,
        Path           NVARCHAR(512)    NULL,
        IpAddress      NVARCHAR(64)     NULL,
        UserAgent      NVARCHAR(512)    NULL,
        OldValues      NVARCHAR(MAX)    NULL,
        NewValues      NVARCHAR(MAX)    NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_OccurredOnUtc' AND object_id = OBJECT_ID(N'dbo.AuditLog'))
BEGIN
    CREATE INDEX IX_AuditLog_OccurredOnUtc ON dbo.AuditLog (OccurredOnUtc DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_Entity' AND object_id = OBJECT_ID(N'dbo.AuditLog'))
BEGIN
    CREATE INDEX IX_AuditLog_Entity ON dbo.AuditLog (EntityType, EntityId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_Actor' AND object_id = OBJECT_ID(N'dbo.AuditLog'))
BEGIN
    CREATE INDEX IX_AuditLog_Actor ON dbo.AuditLog (ActorType, ActorId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_CorrelationId' AND object_id = OBJECT_ID(N'dbo.AuditLog'))
BEGIN
    CREATE INDEX IX_AuditLog_CorrelationId ON dbo.AuditLog (CorrelationId);
END
GO
