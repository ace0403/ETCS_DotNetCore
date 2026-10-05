/*
Deploy on MealDB.

Adds allergen consent audit columns on OrderItem.
Consent is captured when the parent clicks "Agree to Serve" at order time.
No allergen-name snapshot is stored (allergies may change later).
*/
SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF COL_LENGTH('dbo.OrderItem', 'HasAllergenConsent') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrderItem]
        ADD [HasAllergenConsent] BIT NOT NULL
            CONSTRAINT [DF_OrderItem_HasAllergenConsent] DEFAULT (0);
    PRINT 'Added OrderItem.HasAllergenConsent';
END
ELSE
BEGIN
    PRINT 'OrderItem.HasAllergenConsent already exists.';
END
GO

IF COL_LENGTH('dbo.OrderItem', 'ConsentedOn') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrderItem]
        ADD [ConsentedOn] DATETIME NULL;
    PRINT 'Added OrderItem.ConsentedOn';
END
ELSE
BEGIN
    PRINT 'OrderItem.ConsentedOn already exists.';
END
GO
