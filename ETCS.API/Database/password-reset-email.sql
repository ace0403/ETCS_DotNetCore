USE MealDB;
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
