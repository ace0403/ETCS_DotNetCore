-- Optional update for meal order summary report to include POS OrderTypeId (43).
-- Execute manually in MealDB after reviewing spMealOrderSummary_MealDB_New.sql.

/*
-- Example filter addition inside the report procedure:
-- AND o.OrderTypeId IN (24, 42, 43)  -- MealOrder, A La Carte, POS
*/
