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
    @OtpCode NVARCHAR(20) = N'',
    @AddChildLink NVARCHAR(1000) = N'',
    @LogoUrl NVARCHAR(1000) = N'',
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
    SET @Subject = REPLACE(@Subject, '{{OtpCode}}', ISNULL(@OtpCode, N''));
    SET @Subject = REPLACE(@Subject, '{{AddChildLink}}', ISNULL(@AddChildLink, N''));
    SET @Subject = REPLACE(@Subject, '{{LogoUrl}}', ISNULL(@LogoUrl, N''));

    SET @Body = REPLACE(@Body, '{{GuardianName}}', ISNULL(@GuardianName, N''));
    SET @Body = REPLACE(@Body, '{{StudentName}}', ISNULL(@StudentName, N''));
    SET @Body = REPLACE(@Body, '{{OrderId}}', ISNULL(@OrderId, N''));
    SET @Body = REPLACE(@Body, '{{TransactionId}}', ISNULL(@TransactionId, N''));
    SET @Body = REPLACE(@Body, '{{Amount}}', ISNULL(@Amount, N''));
    SET @Body = REPLACE(@Body, '{{EventDate}}', ISNULL(@EventDate, N''));
    SET @Body = REPLACE(@Body, '{{OrderItems}}', ISNULL(@OrderItems, N''));
    SET @Body = REPLACE(@Body, '{{ResetLink}}', ISNULL(@ResetLink, N''));
    SET @Body = REPLACE(@Body, '{{ExpiryMinutes}}', ISNULL(@ExpiryMinutes, N''));
    SET @Body = REPLACE(@Body, '{{OtpCode}}', ISNULL(@OtpCode, N''));
    SET @Body = REPLACE(@Body, '{{AddChildLink}}', ISNULL(@AddChildLink, N''));
    SET @Body = REPLACE(@Body, '{{LogoUrl}}', ISNULL(@LogoUrl, N''));

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
        SSL AS Ssl,
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

    -- Reclaim rows left in Sending if a worker crashed mid-send (SentOn used as claim timestamp).
    UPDATE dbo.EmailNotification
    SET Status = N'Queued',
        SentOn = NULL,
        ErrorMessage = NULL
    WHERE Status = N'Sending'
      AND (
            SentOn IS NULL
            OR SentOn < DATEADD(MINUTE, -15, SYSUTCDATETIME())
          );

    ;WITH cte AS
    (
        SELECT TOP (@BatchSize) Id
        FROM dbo.EmailNotification WITH (UPDLOCK, READPAST, ROWLOCK)
        WHERE Status = N'Queued'
        ORDER BY CreatedOn
    )
    UPDATE e
    SET Status = N'Sending',
        SentOn = SYSUTCDATETIME(),
        ErrorMessage = NULL
    OUTPUT
        inserted.Id,
        inserted.TemplateKey,
        inserted.ToEmail,
        inserted.Subject,
        inserted.BodyHtml,
        inserted.PayloadJson,
        inserted.Status,
        inserted.CreatedOn
    FROM dbo.EmailNotification e
    INNER JOIN cte ON cte.Id = e.Id;
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

EXEC dbo.spUpsertEmailTemplate
    @TemplateKey = N'RegistrationOtp',
    @SubjectTemplate = N'Your ETCS verification code',
    @BodyHtmlTemplate = N'<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Verify your email</title>
</head>
<body style="margin:0;padding:0;background-color:#f5f7f9;-webkit-text-size-adjust:100%;">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#f5f7f9;padding:24px 12px;">
  <tr>
    <td align="center">
      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:600px;background-color:#ffffff;border-radius:12px;overflow:hidden;border:1px solid rgba(15,23,42,0.08);">
        <tr>
          <td align="center" style="background-color:#ffffff;padding:28px 24px 20px 24px;border-bottom:3px solid #3498db;">
            <img src="{{LogoUrl}}" alt="ETCS" width="150" style="display:block;margin:0 auto;max-width:150px;height:auto;border:0;" />
          </td>
        </tr>
        <tr>
          <td style="padding:32px 28px 8px 28px;font-family:Segoe UI,Arial,Helvetica,sans-serif;color:#0f172a;">
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:0 auto 16px auto;">
              <tr>
                <td align="center" style="width:56px;height:56px;border-radius:28px;background-color:#ebf5fb;color:#3498db;font-size:26px;line-height:56px;font-weight:700;">&#9993;</td>
              </tr>
            </table>
            <h1 style="margin:0 0 8px 0;font-size:24px;line-height:1.3;color:#0f172a;text-align:center;font-weight:700;">Verify your email</h1>
            <p style="margin:0 0 20px 0;font-size:15px;line-height:1.6;color:#64748b;text-align:center;">Use the verification code below to complete your ETCS parent account registration.</p>
            <p style="margin:0 0 24px 0;font-size:15px;line-height:1.6;color:#0f172a;">Dear <strong>{{GuardianName}}</strong>,</p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#ebf5fb;border:1px solid rgba(52,152,219,0.28);border-radius:10px;margin:0 0 24px 0;">
              <tr>
                <td style="padding:24px 22px;font-family:Segoe UI,Arial,Helvetica,sans-serif;text-align:center;">
                  <p style="margin:0 0 10px 0;font-size:12px;letter-spacing:0.06em;text-transform:uppercase;color:#2980b9;font-weight:700;">Verification Code</p>
                  <p style="margin:0;font-size:36px;letter-spacing:10px;font-weight:700;color:#0f172a;line-height:1.2;">{{OtpCode}}</p>
                </td>
              </tr>
            </table>
            <p style="margin:0 0 8px 0;font-size:14px;line-height:1.6;color:#64748b;text-align:center;">This code expires in <strong style="color:#0f172a;">{{ExpiryMinutes}}</strong> minutes.</p>
            <p style="margin:0 0 8px 0;font-size:14px;line-height:1.6;color:#64748b;text-align:center;">If you did not start registration, you can ignore this email.</p>
          </td>
        </tr>
        <tr>
          <td style="padding:20px 28px 28px 28px;border-top:1px solid rgba(15,23,42,0.08);font-family:Segoe UI,Arial,Helvetica,sans-serif;background-color:#f8fafc;">
            <p style="margin:0 0 6px 0;font-size:13px;line-height:1.5;color:#64748b;text-align:center;">Need help? Please contact your school administration.</p>
            <p style="margin:0;font-size:12px;line-height:1.5;color:#94a3b8;text-align:center;">Emirates Taste Catering Services<br />ETCS Parent Portal</p>
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>
</body>
</html>',
    @IsActive = 1;
GO

EXEC dbo.spUpsertEmailTemplate
    @TemplateKey = N'RegistrationSuccess',
    @SubjectTemplate = N'Registration successful - Welcome to ETCS Parent Portal',
    @BodyHtmlTemplate = N'<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="utf-8" />
<meta name="viewport" content="width=device-width, initial-scale=1" />
<title>Registration Successful</title>
</head>
<body style="margin:0;padding:0;background-color:#f5f7f9;-webkit-text-size-adjust:100%;">
<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#f5f7f9;padding:24px 12px;">
  <tr>
    <td align="center">
      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="max-width:600px;background-color:#ffffff;border-radius:12px;overflow:hidden;border:1px solid rgba(15,23,42,0.08);">
        <tr>
          <td align="center" style="background-color:#ffffff;padding:28px 24px 20px 24px;border-bottom:3px solid #3498db;">
            <img src="{{LogoUrl}}" alt="ETCS" width="150" style="display:block;margin:0 auto;max-width:150px;height:auto;border:0;" />
          </td>
        </tr>
        <tr>
          <td style="padding:32px 28px 8px 28px;font-family:Segoe UI,Arial,Helvetica,sans-serif;color:#0f172a;">
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:0 auto 16px auto;">
              <tr>
                <td align="center" style="width:56px;height:56px;border-radius:28px;background-color:#ebf5fb;color:#3498db;font-size:28px;line-height:56px;font-weight:700;">&#10003;</td>
              </tr>
            </table>
            <h1 style="margin:0 0 8px 0;font-size:24px;line-height:1.3;color:#0f172a;text-align:center;font-weight:700;">Registration Successful!</h1>
            <p style="margin:0 0 20px 0;font-size:15px;line-height:1.6;color:#64748b;text-align:center;">Your ETCS Parent Portal account has been created successfully.</p>
            <p style="margin:0 0 24px 0;font-size:15px;line-height:1.6;color:#0f172a;">Dear <strong>{{GuardianName}}</strong>,</p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="background-color:#ebf5fb;border:1px solid rgba(52,152,219,0.28);border-radius:10px;margin:0 0 24px 0;">
              <tr>
                <td style="padding:20px 22px;font-family:Segoe UI,Arial,Helvetica,sans-serif;">
                  <p style="margin:0 0 6px 0;font-size:12px;letter-spacing:0.06em;text-transform:uppercase;color:#2980b9;font-weight:700;">Add Your Child</p>
                  <p style="margin:0 0 10px 0;font-size:15px;line-height:1.6;color:#0f172a;">To complete your setup, please add your child&rsquo;s details to your account.</p>
                  <p style="margin:0;font-size:14px;line-height:1.6;color:#64748b;">You can add one or more children and manage their information from your Parent Portal.</p>
                </td>
              </tr>
            </table>
            <p style="margin:0 0 12px 0;font-size:14px;font-weight:700;color:#0f172a;text-align:center;">Next Step</p>
            <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:0 auto 20px auto;">
              <tr>
                <td align="center" style="border-radius:8px;background-color:#3498db;">
                  <a href="{{AddChildLink}}" style="display:inline-block;padding:14px 28px;font-family:Segoe UI,Arial,Helvetica,sans-serif;font-size:15px;font-weight:700;color:#ffffff;text-decoration:none;border-radius:8px;">Add Child Details</a>
                </td>
              </tr>
            </table>
            <p style="margin:0 0 8px 0;font-size:14px;line-height:1.6;color:#64748b;text-align:center;">You can also add your child later from the <strong style="color:#0f172a;">My Children</strong> section.</p>
          </td>
        </tr>
        <tr>
          <td style="padding:20px 28px 28px 28px;border-top:1px solid rgba(15,23,42,0.08);font-family:Segoe UI,Arial,Helvetica,sans-serif;background-color:#f8fafc;">
            <p style="margin:0 0 6px 0;font-size:13px;line-height:1.5;color:#64748b;text-align:center;">Need help? Please contact your school administration.</p>
            <p style="margin:0;font-size:12px;line-height:1.5;color:#94a3b8;text-align:center;">Emirates Taste Catering Services<br />ETCS Parent Portal</p>
          </td>
        </tr>
      </table>
    </td>
  </tr>
</table>
</body>
</html>',
    @IsActive = 1;
GO
