-- Reference: legacy ibonus procedure (deploy only if not already present).
-- Spend-limit rollback after failed cashless checkout (iBonus 5002 already charged access log).
-- Parameters from clsPoss.UpdateAccelogIDmember():
--   @customerid, @amount

-- EXEC spDeleteAccesslogBylimit
--   @customerid = N'1234567890123456',
--   @amount = 25.00;
