using ETCS.Shared.Enumeration;

namespace ETCS.Shared.Application.Email;

internal static class EmailTemplateKeyResolver
{
    public static string ResolveForOrderType(int orderTypeId) =>
        orderTypeId switch
        {
            (int)TransactionTypeEnum.A_La_Carte => EmailTemplateKeys.AlaCarteOrderSuccess,
            (int)TransactionTypeEnum.MealOrder => EmailTemplateKeys.MealComboOrderSuccess,
            _ => EmailTemplateKeys.MealComboOrderSuccess
        };
}
