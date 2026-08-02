USE MealDB;
GO

IF OBJECT_ID(N'dbo.Notification', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Notification
    (
        Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notification PRIMARY KEY,
        GuardianId INT NOT NULL,
        StudentId INT NULL,
        SchoolId INT NULL,
        [Type] NVARCHAR(50) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        [Message] NVARCHAR(1000) NOT NULL,
        ReferenceType NVARCHAR(50) NULL,
        ReferenceId NVARCHAR(100) NULL,
        IsRead BIT NOT NULL CONSTRAINT DF_Notification_IsRead DEFAULT (0),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_Notification_CreatedOn DEFAULT (SYSUTCDATETIME()),
        ReadOn DATETIME2 NULL,
        CreatedBy NVARCHAR(100) NULL
    );

    CREATE INDEX IX_Notification_Guardian_CreatedOn
        ON dbo.Notification (GuardianId, CreatedOn DESC);

    CREATE INDEX IX_Notification_Guardian_IsRead
        ON dbo.Notification (GuardianId, IsRead)
        INCLUDE (CreatedOn);

    CREATE INDEX IX_Notification_School_CreatedOn
        ON dbo.Notification (SchoolId, CreatedOn DESC)
        WHERE SchoolId IS NOT NULL;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spCreateGuardianNotification
    @GuardianId INT,
    @StudentId INT = NULL,
    @SchoolId INT = NULL,
    @Type NVARCHAR(50),
    @Title NVARCHAR(200),
    @Message NVARCHAR(1000),
    @ReferenceType NVARCHAR(50) = NULL,
    @ReferenceId NVARCHAR(100) = NULL,
    @CreatedBy NVARCHAR(100) = N'System'
AS
BEGIN
    SET NOCOUNT ON;

    IF @GuardianId IS NULL OR @GuardianId <= 0
    BEGIN
        THROW 50010, 'GuardianId is required.', 1;
    END

    IF NULLIF(LTRIM(RTRIM(@Type)), N'') IS NULL
       OR NULLIF(LTRIM(RTRIM(@Title)), N'') IS NULL
       OR NULLIF(LTRIM(RTRIM(@Message)), N'') IS NULL
    BEGIN
        THROW 50011, 'Type, Title and Message are required.', 1;
    END

    INSERT INTO dbo.Notification
    (
        GuardianId,
        StudentId,
        SchoolId,
        [Type],
        Title,
        [Message],
        ReferenceType,
        ReferenceId,
        CreatedBy
    )
    VALUES
    (
        @GuardianId,
        @StudentId,
        @SchoolId,
        @Type,
        @Title,
        @Message,
        @ReferenceType,
        @ReferenceId,
        ISNULL(NULLIF(LTRIM(RTRIM(@CreatedBy)), N''), N'System')
    );

    SELECT CAST(SCOPE_IDENTITY() AS BIGINT) AS Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spGetGuardianNotifications
    @GuardianId INT,
    @Page INT = 1,
    @PageSize INT = 50,
    @UnreadOnly BIT = 0
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page IS NULL OR @Page <= 0 SET @Page = 1;
    IF @PageSize IS NULL OR @PageSize <= 0 SET @PageSize = 50;
    IF @PageSize > 100 SET @PageSize = 100;

    DECLARE @Offset INT = (@Page - 1) * @PageSize;

    SELECT COUNT(1) AS TotalCount
    FROM dbo.Notification n
    WHERE n.GuardianId = @GuardianId
      AND (@UnreadOnly = 0 OR n.IsRead = 0);

    SELECT
        n.Id,
        n.GuardianId,
        n.StudentId,
        n.SchoolId,
        n.[Type],
        n.Title,
        n.[Message],
        n.ReferenceType,
        n.ReferenceId,
        n.IsRead,
        n.CreatedOn,
        n.ReadOn,
        n.CreatedBy
    FROM dbo.Notification n
    WHERE n.GuardianId = @GuardianId
      AND (@UnreadOnly = 0 OR n.IsRead = 0)
    ORDER BY n.CreatedOn DESC, n.Id DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spGetGuardianNotificationById
    @GuardianId INT,
    @NotificationId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        n.Id,
        n.GuardianId,
        n.StudentId,
        n.SchoolId,
        n.[Type],
        n.Title,
        n.[Message],
        n.ReferenceType,
        n.ReferenceId,
        n.IsRead,
        n.CreatedOn,
        n.ReadOn,
        n.CreatedBy
    FROM dbo.Notification n
    WHERE n.GuardianId = @GuardianId
      AND n.Id = @NotificationId;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spGetGuardianUnreadNotificationCount
    @GuardianId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(1) AS UnreadCount
    FROM dbo.Notification
    WHERE GuardianId = @GuardianId
      AND IsRead = 0;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spMarkGuardianNotificationRead
    @GuardianId INT,
    @NotificationId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Notification
    SET IsRead = 1,
        ReadOn = SYSUTCDATETIME()
    WHERE Id = @NotificationId
      AND GuardianId = @GuardianId
      AND IsRead = 0;

    SELECT @@ROWCOUNT AS UpdatedCount;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spMarkAllGuardianNotificationsRead
    @GuardianId INT
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.Notification
    SET IsRead = 1,
        ReadOn = SYSUTCDATETIME()
    WHERE GuardianId = @GuardianId
      AND IsRead = 0;

    SELECT @@ROWCOUNT AS UpdatedCount;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spAdminGetNotificationLog
    @Top INT = 200,
    @GuardianId INT = NULL,
    @SchoolId INT = NULL,
    @Type NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Top IS NULL OR @Top <= 0 SET @Top = 200;
    IF @Top > 500 SET @Top = 500;

    SELECT TOP (@Top)
        n.Id,
        n.GuardianId,
        n.StudentId,
        n.SchoolId,
        n.[Type],
        n.Title,
        n.[Message],
        n.ReferenceType,
        n.ReferenceId,
        n.IsRead,
        n.CreatedOn,
        n.ReadOn,
        n.CreatedBy
    FROM dbo.Notification n
    WHERE (@GuardianId IS NULL OR n.GuardianId = @GuardianId)
      AND (@SchoolId IS NULL OR n.SchoolId = @SchoolId)
      AND (@Type IS NULL OR @Type = N'' OR n.[Type] = @Type)
    ORDER BY n.CreatedOn DESC, n.Id DESC;
END;
GO
