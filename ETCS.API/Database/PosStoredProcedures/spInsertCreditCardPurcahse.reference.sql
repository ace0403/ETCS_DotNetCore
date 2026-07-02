-- Reference: legacy ibonus procedure (deploy only if not already present).
-- Parameters from clsPoss.InsertCreditCardPurcahse():
--   @Amount, @branchCode, @terminalcode, @transactionid, @CreditCardNumber

-- EXEC spInsertCreditCardPurcahse
--   @Amount = 20.00,
--   @branchCode = '1',
--   @terminalcode = 1,
--   @transactionid = '10609062600',
--   @CreditCardNumber = '4111111111111111';
