-- MealDB: store payment instrument used for gateway transactions.
-- Run against MealDB before deploying native EPG v2 payment flows.
--
-- PaymentMethod values (see ETCS.Shared.Enumeration.PaymentMethodEnum):
--   0 = Unknown
--   1 = Card
--   2 = ApplePay
--   3 = SamsungPay
--   4 = WebRedirect (legacy WebView redirect flow)
--
-- Legacy rows remain NULL / Unknown. v1 WebView payments may be backfilled as WebRedirect later.

USE MealDB;
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'[Transaction]')
      AND name = N'PaymentMethod')
BEGIN
    ALTER TABLE [Transaction]
    ADD PaymentMethod int NULL;
    -- 0=Unknown, 1=Card, 2=ApplePay, 3=SamsungPay, 4=WebRedirect
END
GO
