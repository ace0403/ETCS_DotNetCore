# POS legacy stored procedures (ibonus)

These procedures already exist in the legacy **ibonus** database (`AVIeRewords` connection).
The web POS calls them via `PosLegacyTransactionRepository` — same as `clsPoss.cs` / `AppHandler.ashx`.

Deploy only if missing from your environment. Parameter names match legacy `App_Code/clsPoss.cs`.

| Procedure | Used by |
|-----------|---------|
| `spInsertCashPurcahse` | Cash |
| `spUndoCashPurhcase` | Undo Cash |
| `spInsertCreditCardPurcahse` | Credit/Debit Card |
| `spInsertWindposPurchase` | Cashless (per cart line, `skucode` = `ItemMaster.ItemCode`) |
| `spDeleteAccesslogBylimit` | Cashless spend-limit rollback after iBonus 5002 |
| `spGetSpendLimitInfo` | Cashless spend check after iBonus 5002 |
| `spGetItemCodeByItemId` | Catalog ItemCode fallback |
