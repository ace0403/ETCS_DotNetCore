USE MealDB;
GO

IF COL_LENGTH(N'dbo.Transaction', N'ReconcileAttemptCount') IS NULL
BEGIN
    ALTER TABLE dbo.[Transaction]
        ADD ReconcileAttemptCount INT NOT NULL
            CONSTRAINT DF_Transaction_ReconcileAttemptCount DEFAULT (0);
END;
GO

IF COL_LENGTH(N'dbo.Transaction', N'LastReconcileOn') IS NULL
BEGIN
    ALTER TABLE dbo.[Transaction]
        ADD LastReconcileOn DATETIME2 NULL;
END;
GO
