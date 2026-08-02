USE MealDB;
GO

IF OBJECT_ID(N'dbo.EmailTemplate', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailTemplate
    (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TemplateKey NVARCHAR(100) NOT NULL UNIQUE,
        SubjectTemplate NVARCHAR(500) NOT NULL,
        BodyHtmlTemplate NVARCHAR(MAX) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_EmailTemplate_IsActive DEFAULT (1),
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_EmailTemplate_CreatedOn DEFAULT (SYSUTCDATETIME()),
        UpdatedOn DATETIME2 NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.EmailNotification', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmailNotification
    (
        Id BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        TemplateKey NVARCHAR(100) NOT NULL,
        ToEmail NVARCHAR(256) NOT NULL,
        Subject NVARCHAR(500) NOT NULL,
        BodyHtml NVARCHAR(MAX) NOT NULL,
        PayloadJson NVARCHAR(MAX) NULL,
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_EmailNotification_Status DEFAULT (N'Queued'),
        ErrorMessage NVARCHAR(2000) NULL,
        CreatedOn DATETIME2 NOT NULL CONSTRAINT DF_EmailNotification_CreatedOn DEFAULT (SYSUTCDATETIME()),
        SentOn DATETIME2 NULL
    );
END;
GO

IF OBJECT_ID(N'dbo.SmtpSettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SmtpSettings
    (
        Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_SmtpSettings PRIMARY KEY,
        SmtpHost NVARCHAR(500) NOT NULL,
        SmtpEmail NVARCHAR(500) NOT NULL,
        Password NVARCHAR(500) NOT NULL,
        SSL BIT NOT NULL CONSTRAINT DF_SmtpSettings_SSL DEFAULT (1),
        Port INT NOT NULL CONSTRAINT DF_SmtpSettings_Port DEFAULT (587)
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.spUpsertEmailTemplate
    @TemplateKey NVARCHAR(100),
    @SubjectTemplate NVARCHAR(500),
    @BodyHtmlTemplate NVARCHAR(MAX),
    @IsActive BIT = 1
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.EmailTemplate WHERE TemplateKey = @TemplateKey)
    BEGIN
        UPDATE dbo.EmailTemplate
        SET SubjectTemplate = @SubjectTemplate,
            BodyHtmlTemplate = @BodyHtmlTemplate,
            IsActive = @IsActive,
            UpdatedOn = SYSUTCDATETIME()
        WHERE TemplateKey = @TemplateKey;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.EmailTemplate (TemplateKey, SubjectTemplate, BodyHtmlTemplate, IsActive)
        VALUES (@TemplateKey, @SubjectTemplate, @BodyHtmlTemplate, @IsActive);
    END
END;
GO

CREATE OR ALTER PROCEDURE dbo.spQueueEmailNotification
    @TemplateKey NVARCHAR(100),
    @ToEmail NVARCHAR(256),
    @GuardianName NVARCHAR(200) = N'',
    @StudentName NVARCHAR(200) = N'',
    @OrderId NVARCHAR(100) = N'',
    @TransactionId NVARCHAR(200) = N'',
    @Amount NVARCHAR(50) = N'',
    @EventDate NVARCHAR(100) = N'',
    @OrderItems NVARCHAR(MAX) = N'',
    @ResetLink NVARCHAR(1000) = N'',
    @ExpiryMinutes NVARCHAR(20) = N'',
    @PayloadJson NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @SubjectTemplate NVARCHAR(500);
    DECLARE @BodyTemplate NVARCHAR(MAX);
    DECLARE @Subject NVARCHAR(500);
    DECLARE @Body NVARCHAR(MAX);

    SELECT TOP (1)
        @SubjectTemplate = SubjectTemplate,
        @BodyTemplate = BodyHtmlTemplate
    FROM dbo.EmailTemplate
    WHERE TemplateKey = @TemplateKey
      AND IsActive = 1;

    IF @SubjectTemplate IS NULL OR @BodyTemplate IS NULL
    BEGIN
        THROW 50001, 'Email template not found or inactive.', 1;
    END

    SET @Subject = @SubjectTemplate;
    SET @Body = @BodyTemplate;

    SET @Subject = REPLACE(@Subject, '{{GuardianName}}', ISNULL(@GuardianName, N''));
    SET @Subject = REPLACE(@Subject, '{{StudentName}}', ISNULL(@StudentName, N''));
    SET @Subject = REPLACE(@Subject, '{{OrderId}}', ISNULL(@OrderId, N''));
    SET @Subject = REPLACE(@Subject, '{{TransactionId}}', ISNULL(@TransactionId, N''));
    SET @Subject = REPLACE(@Subject, '{{Amount}}', ISNULL(@Amount, N''));
    SET @Subject = REPLACE(@Subject, '{{EventDate}}', ISNULL(@EventDate, N''));
    SET @Subject = REPLACE(@Subject, '{{OrderItems}}', ISNULL(@OrderItems, N''));
    SET @Subject = REPLACE(@Subject, '{{ResetLink}}', ISNULL(@ResetLink, N''));
    SET @Subject = REPLACE(@Subject, '{{ExpiryMinutes}}', ISNULL(@ExpiryMinutes, N''));

    SET @Body = REPLACE(@Body, '{{GuardianName}}', ISNULL(@GuardianName, N''));
    SET @Body = REPLACE(@Body, '{{StudentName}}', ISNULL(@StudentName, N''));
    SET @Body = REPLACE(@Body, '{{OrderId}}', ISNULL(@OrderId, N''));
    SET @Body = REPLACE(@Body, '{{TransactionId}}', ISNULL(@TransactionId, N''));
    SET @Body = REPLACE(@Body, '{{Amount}}', ISNULL(@Amount, N''));
    SET @Body = REPLACE(@Body, '{{EventDate}}', ISNULL(@EventDate, N''));
    SET @Body = REPLACE(@Body, '{{OrderItems}}', ISNULL(@OrderItems, N''));
    SET @Body = REPLACE(@Body, '{{ResetLink}}', ISNULL(@ResetLink, N''));
    SET @Body = REPLACE(@Body, '{{ExpiryMinutes}}', ISNULL(@ExpiryMinutes, N''));

    INSERT INTO dbo.EmailNotification
    (
        TemplateKey,
        ToEmail,
        Subject,
        BodyHtml,
        PayloadJson,
        Status,
        CreatedOn
    )
    VALUES
    (
        @TemplateKey,
        @ToEmail,
        @Subject,
        @Body,
        @PayloadJson,
        N'Queued',
        SYSUTCDATETIME()
    );
END;
GO

CREATE OR ALTER PROCEDURE dbo.spMarkEmailNotificationStatus
    @Id BIGINT,
    @Status NVARCHAR(20),
    @ErrorMessage NVARCHAR(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.EmailNotification
    SET Status = @Status,
        ErrorMessage = @ErrorMessage,
        SentOn = CASE WHEN @Status = N'Sent' THEN SYSUTCDATETIME() ELSE SentOn END
    WHERE Id = @Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spGetEmailTemplates
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id,
        TemplateKey,
        SubjectTemplate,
        IsActive,
        CreatedOn,
        UpdatedOn
    FROM dbo.EmailTemplate
    ORDER BY TemplateKey;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spGetEmailTemplateByKey
    @TemplateKey NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT Id,
        TemplateKey,
        SubjectTemplate,
        BodyHtmlTemplate,
        IsActive,
        CreatedOn,
        UpdatedOn
    FROM dbo.EmailTemplate
    WHERE TemplateKey = @TemplateKey;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spGetSmtpSettings
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (1)
        Id,
        SmtpHost,
        SmtpEmail,
        Password,
        SSL,
        Port
    FROM dbo.SmtpSettings
    ORDER BY Id;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spUpsertSmtpSettings
    @Id INT = NULL,
    @SmtpHost NVARCHAR(500),
    @SmtpEmail NVARCHAR(500),
    @Password NVARCHAR(500),
    @SSL BIT = 1,
    @Port INT = 587
AS
BEGIN
    SET NOCOUNT ON;

    IF @Id IS NOT NULL AND EXISTS (SELECT 1 FROM dbo.SmtpSettings WHERE Id = @Id)
    BEGIN
        UPDATE dbo.SmtpSettings
        SET SmtpHost = @SmtpHost,
            SmtpEmail = @SmtpEmail,
            Password = @Password,
            SSL = @SSL,
            Port = @Port
        WHERE Id = @Id;
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM dbo.SmtpSettings)
    BEGIN
        UPDATE dbo.SmtpSettings
        SET SmtpHost = @SmtpHost,
            SmtpEmail = @SmtpEmail,
            Password = @Password,
            SSL = @SSL,
            Port = @Port
        WHERE Id = (SELECT TOP (1) Id FROM dbo.SmtpSettings ORDER BY Id);
        RETURN;
    END

    INSERT INTO dbo.SmtpSettings (SmtpHost, SmtpEmail, Password, SSL, Port)
    VALUES (@SmtpHost, @SmtpEmail, @Password, @SSL, @Port);
END;
GO

CREATE OR ALTER PROCEDURE dbo.spGetPendingEmailNotifications
    @BatchSize INT = 20
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@BatchSize)
        Id,
        TemplateKey,
        ToEmail,
        Subject,
        BodyHtml,
        PayloadJson,
        Status,
        CreatedOn
    FROM dbo.EmailNotification
    WHERE Status = N'Queued'
    ORDER BY CreatedOn;
END;
GO

CREATE OR ALTER PROCEDURE dbo.spGetEmailNotificationLog
    @Top INT = 200
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Top)
        Id,
        TemplateKey,
        ToEmail,
        Subject,
        Status,
        ErrorMessage,
        CreatedOn,
        SentOn
    FROM dbo.EmailNotification
    ORDER BY CreatedOn DESC;
END;
GO

-- Default templates (safe to re-run; updates subject/body for known keys)
EXEC dbo.spUpsertEmailTemplate
    @TemplateKey = N'TopupSuccess',
    @SubjectTemplate = N'Wallet top-up successful for {{StudentName}}',
    @BodyHtmlTemplate = N'<div style="font-family:Segoe UI,Arial,sans-serif;max-width:600px;margin:0 auto;color:#333;">
<h2 style="color:#fea116;margin-bottom:8px;">Top-up Successful</h2>
<p>Dear {{GuardianName}},</p>
<p>Your wallet top-up for <strong>{{StudentName}}</strong> was completed successfully.</p>
<table style="width:100%;border-collapse:collapse;margin:16px 0;">
<tr><td style="padding:8px;border:1px solid #eee;background:#fafafa;width:40%;">Amount</td><td style="padding:8px;border:1px solid #eee;"><strong>{{Amount}}</strong></td></tr>
<tr><td style="padding:8px;border:1px solid #eee;background:#fafafa;">Reference</td><td style="padding:8px;border:1px solid #eee;">{{OrderId}}</td></tr>
<tr><td style="padding:8px;border:1px solid #eee;background:#fafafa;">Transaction ID</td><td style="padding:8px;border:1px solid #eee;">{{TransactionId}}</td></tr>
<tr><td style="padding:8px;border:1px solid #eee;background:#fafafa;">Date</td><td style="padding:8px;border:1px solid #eee;">{{EventDate}}</td></tr>
</table>
<p style="color:#666;font-size:13px;">Thank you for using ETCS.</p>
</div>',
    @IsActive = 1;
GO

EXEC dbo.spUpsertEmailTemplate
    @TemplateKey = N'AlaCarteOrderSuccess',
    @SubjectTemplate = N'Ala-Carte order confirmed for {{StudentName}}',
    @BodyHtmlTemplate = N'<div style="font-family:Segoe UI,Arial,sans-serif;max-width:600px;margin:0 auto;color:#333;">
<h2 style="color:#fea116;margin-bottom:8px;">Ala-Carte Order Confirmed</h2>
<p>Dear {{GuardianName}},</p>
<p>Your ala-carte order for <strong>{{StudentName}}</strong> has been placed successfully.</p>
<table style="width:100%;border-collapse:collapse;margin:16px 0;">
<tr><td style="padding:8px;border:1px solid #eee;background:#fafafa;width:40%;">Order ID</td><td style="padding:8px;border:1px solid #eee;">{{OrderId}}</td></tr>
<tr><td style="padding:8px;border:1px solid #eee;background:#fafafa;">Transaction ID</td><td style="padding:8px;border:1px solid #eee;">{{TransactionId}}</td></tr>
<tr><td style="padding:8px;border:1px solid #eee;background:#fafafa;">Total</td><td style="padding:8px;border:1px solid #eee;"><strong>{{Amount}}</strong></td></tr>
<tr><td style="padding:8px;border:1px solid #eee;background:#fafafa;">Date</td><td style="padding:8px;border:1px solid #eee;">{{EventDate}}</td></tr>
</table>
<h3 style="margin-top:20px;font-size:16px;">Order Items</h3>
<div style="padding:12px;border:1px solid #eee;border-radius:4px;background:#fafafa;">{{OrderItems}}</div>
<p style="color:#666;font-size:13px;">Thank you for your order.</p>
</div>',
    @IsActive = 1;
GO

EXEC dbo.spUpsertEmailTemplate
    @TemplateKey = N'MealComboOrderSuccess',
    @SubjectTemplate = N'Meal combo order confirmed for {{StudentName}}',
    @BodyHtmlTemplate = N'<div style="font-family:Segoe UI,Arial,sans-serif;max-width:600px;margin:0 auto;color:#333;">
<h2 style="color:#fea116;margin-bottom:8px;">Meal Combo Order Confirmed</h2>
<p>Dear {{GuardianName}},</p>
<p>Your meal combo order for <strong>{{StudentName}}</strong> has been placed successfully.</p>
<table style="width:100%;border-collapse:collapse;margin:16px 0;">
<tr><td style="padding:8px;border:1px solid #eee;background:#fafafa;width:40%;">Order ID</td><td style="padding:8px;border:1px solid #eee;">{{OrderId}}</td></tr>
<tr><td style="padding:8px;border:1px solid #eee;background:#fafafa;">Transaction ID</td><td style="padding:8px;border:1px solid #eee;">{{TransactionId}}</td></tr>
<tr><td style="padding:8px;border:1px solid #eee;background:#fafafa;">Total</td><td style="padding:8px;border:1px solid #eee;"><strong>{{Amount}}</strong></td></tr>
<tr><td style="padding:8px;border:1px solid #eee;background:#fafafa;">Date</td><td style="padding:8px;border:1px solid #eee;">{{EventDate}}</td></tr>
</table>
<h3 style="margin-top:20px;font-size:16px;">Order Items</h3>
<div style="padding:12px;border:1px solid #eee;border-radius:4px;background:#fafafa;">{{OrderItems}}</div>
<p style="color:#666;font-size:13px;">Thank you for your order.</p>
</div>',
    @IsActive = 1;
GO

EXEC dbo.spUpsertEmailTemplate
    @TemplateKey = N'PasswordReset',
    @SubjectTemplate = N'Reset your ETCS password',
    @BodyHtmlTemplate = N'<div style="font-family:Segoe UI,Arial,sans-serif;max-width:600px;margin:0 auto;color:#333;">
<h2 style="color:#fea116;margin-bottom:8px;">Password Reset</h2>
<p>Dear {{GuardianName}},</p>
<p>We received a request to reset the password for your ETCS account.</p>
<p style="margin:24px 0;">
<a href="{{ResetLink}}" style="display:inline-block;background:#fea116;color:#fff;text-decoration:none;padding:12px 20px;border-radius:6px;font-weight:600;">Reset password</a>
</p>
<p>Or copy and paste this link into your browser:</p>
<p style="word-break:break-all;color:#555;">{{ResetLink}}</p>
<p>This link expires in <strong>{{ExpiryMinutes}}</strong> minutes. If you did not request a password reset, you can ignore this email.</p>
<p style="color:#666;font-size:13px;">Emirates Taste Catering Services</p>
</div>',
    @IsActive = 1;
GO
