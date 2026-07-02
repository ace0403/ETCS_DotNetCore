/*
Deploy on MealDB database.
Adds ProcessingFee to MealPackages for meal combo pricing.
*/
USE MealDB;
GO

IF COL_LENGTH('dbo.MealPackages', 'ProcessingFee') IS NULL
BEGIN
    ALTER TABLE dbo.MealPackages
    ADD ProcessingFee DECIMAL(18, 2) NOT NULL CONSTRAINT DF_MealPackages_ProcessingFee DEFAULT (0);
END
GO
