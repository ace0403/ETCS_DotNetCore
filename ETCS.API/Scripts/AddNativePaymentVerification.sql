-- Run against MealDB before deploying the native/V2 gateway update.
-- Additive native-only journal. V1/web tables, stored procedures and processing are unchanged.
-- Do not auto-backfill capture flags for existing payments: verify historical payments with EPG first.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.NativePaymentVerification', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NativePaymentVerification
    (
        TransactionPkId int NOT NULL CONSTRAINT PK_NativePaymentVerification PRIMARY KEY,
        OrderId nvarchar(64) NOT NULL,
        Kind varchar(5) NOT NULL,
        GatewayTransactionId nvarchar(128) NOT NULL,
        Amount decimal(19,4) NOT NULL,
        Currency char(3) NOT NULL,
        ManualCapture bit NOT NULL,
        CaptureAttemptedAt datetime2 NULL,
        CapturedAt datetime2 NULL,
        CancellationAttemptedAt datetime2 NULL,
        CancelledAt datetime2 NULL,
        CancellationProofJson nvarchar(max) NULL,
        DeclinedAt datetime2 NULL,
        DeclineCode varchar(16) NULL,
        ProofJson nvarchar(max) NULL,
        CreatedAt datetime2 NOT NULL CONSTRAINT DF_NativeVerification_Created DEFAULT SYSUTCDATETIME(),
        UpdatedAt datetime2 NOT NULL CONSTRAINT DF_NativeVerification_Updated DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UQ_NativeVerification_Gateway UNIQUE (GatewayTransactionId),
        CONSTRAINT CK_NativeVerification_Kind CHECK (Kind IN ('order', 'topup')),
        CONSTRAINT CK_NativeVerification_Amount CHECK (Amount > 0),
        CONSTRAINT CK_NativeVerification_Proof CHECK (ProofJson IS NULL OR ISJSON(ProofJson) = 1)
    );
END;
-- Safe to rerun when the initial native verification journal has already been installed.
IF COL_LENGTH(N'dbo.NativePaymentVerification', N'DeclinedAt') IS NULL
    ALTER TABLE dbo.NativePaymentVerification ADD DeclinedAt datetime2 NULL;
IF COL_LENGTH(N'dbo.NativePaymentVerification', N'DeclineCode') IS NULL
    ALTER TABLE dbo.NativePaymentVerification ADD DeclineCode varchar(16) NULL;
IF COL_LENGTH(N'dbo.NativePaymentVerification', N'CancellationAttemptedAt') IS NULL
    ALTER TABLE dbo.NativePaymentVerification ADD CancellationAttemptedAt datetime2 NULL;
IF COL_LENGTH(N'dbo.NativePaymentVerification', N'CancelledAt') IS NULL
    ALTER TABLE dbo.NativePaymentVerification ADD CancelledAt datetime2 NULL;
IF COL_LENGTH(N'dbo.NativePaymentVerification', N'CancellationProofJson') IS NULL
    ALTER TABLE dbo.NativePaymentVerification ADD CancellationProofJson nvarchar(max) NULL;
COMMIT TRANSACTION;
