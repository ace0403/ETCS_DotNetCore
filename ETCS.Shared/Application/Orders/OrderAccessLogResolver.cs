using ETCS.Shared.Enumeration;

namespace ETCS.Shared.Application.Orders;

internal static class OrderAccessLogResolver
{
    public static (int AccessLogTypeId, string OrderDescription) Resolve(int orderTypeId)
    {
        return orderTypeId switch
        {
            (int)TransactionTypeEnum.A_La_Carte => ((int)AccessLogTypeEnum.A_La_Carte, "A La Carte Order"),
            (int)TransactionTypeEnum.POS => ((int)AccessLogTypeEnum.A_La_Carte, "POS Order"),
            (int)TransactionTypeEnum.MealOrder => ((int)AccessLogTypeEnum.MealOrder, "Meal plan"),
            _ => ((int)AccessLogTypeEnum.MealOrder, "Meal plan")
        };
    }
}
