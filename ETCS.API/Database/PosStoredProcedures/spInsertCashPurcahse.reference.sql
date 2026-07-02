-- Reference: legacy ibonus procedure (deploy only if not already present).
-- Parameters from clsPoss.InsertCashPurcahse():
--   @Amount (float), @branchCode (varchar), @terminalcode (int), @transactionid (varchar)

-- EXEC spInsertCashPurcahse
--   @Amount = 15.00,
--   @branchCode = '1',
--   @terminalcode = 1,
--   @transactionid = '10609062530';
