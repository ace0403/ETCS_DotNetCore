using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ETCS.Shared.Enumeration
{
    public enum TransactionTypeEnum : int
    {
        [Description("Topup Recharge")]
        Topup = 23,
        [Description("Meal Order")]
        MealOrder = 24,
        [Description("A La Carte Order")]
        A_La_Carte = 42,
        [Description("POS Order")]
        POS = 43,
        [Description("Balance Transfer")]
        BalanceTransfer = 65
    }
}
