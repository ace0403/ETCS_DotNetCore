/*
Deploy on MealDB.

Stores comma-separated allergen item names at order time (server-resolved overlap).
Run after OrderItem_AddAllergenConsent.sql.
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.OrderItem', 'AllergenItemText') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrderItem]
        ADD [AllergenItemText] NVARCHAR(500) NULL;
    PRINT 'Added OrderItem.AllergenItemText';
END
ELSE
BEGIN
    PRINT 'OrderItem.AllergenItemText already exists.';
END
GO
