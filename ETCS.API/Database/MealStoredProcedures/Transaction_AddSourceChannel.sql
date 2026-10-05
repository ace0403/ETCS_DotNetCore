/*
Deploy on MealDB.

Stores placement source for orders and top-ups (Api / Web / Pos).
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.Transaction', 'SourceChannel') IS NULL
BEGIN
    ALTER TABLE [dbo].[Transaction]
        ADD [SourceChannel] NVARCHAR(20) NULL;
    PRINT 'Added Transaction.SourceChannel';
END
ELSE
BEGIN
    PRINT 'Transaction.SourceChannel already exists.';
END
GO
