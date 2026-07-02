-- Reference: legacy ibonus procedure (deploy only if not already present).
-- Called from GET /api/pos/students/{customerId}/spend-info after iBonus 5002.
-- Parameters from clsPoss.GetSpendInfo():
--   @customerid, @currentDate (yyyy-MM-dd), @Starttime (week start, Sunday-based)

-- EXEC spGetSpendLimitInfo
--   @customerid = N'1234567890123456',
--   @currentDate = '2026-06-09',
--   @Starttime = '6/8/2026 12:00:00 AM';
