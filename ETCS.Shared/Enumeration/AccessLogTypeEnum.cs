using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace ETCS.Shared.Enumeration
{
    public enum AccessLogTypeEnum : int
    {
        [Description("Topup Recharge")]
        Topup = 10001, // 23,
        [Description("Meal Order")]
        MealOrder = 9001, // 24,
        [Description("A La Carte Order")]
        A_La_Carte = 42,
        [Description("Balance Transfer")]
        BalanceTransfer = 65,
        [Description("Cash Purchase")]
        Cash_Purchase = 1001,
        [Description("Card Purchase")]
        Card_Purchase = 1002,
        [Description("Redeem Points")]
        Redeem_Points = 1003,
        [Description("Reload")]
        Reload = 1004,
        [Description("Undo Cash Purchase")]
        Undo_Cash_Purchase = 1005,
        [Description("Undo Card Purchase")]
        Undo_Card_Purchase = 1006,
        [Description("Undo_Reload")]
        Undo_Reload = 1007,
        [Description("Undo Manual Redeem Points")]
        Undo_Manual_Redeem_Point = 1009,
        [Description("Carry Forward")]
        Carry_Forward = 1010,
        [Description("Positive Adjustment Point")]
        Positive_Adjustment_Point = 1012,
        [Description("Negative Adjustment Point")]
        Negative_Adjustment_Point = 1013,
    }
}
