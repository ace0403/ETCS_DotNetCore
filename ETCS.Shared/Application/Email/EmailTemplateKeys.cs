namespace ETCS.Shared.Application.Email;

public static class EmailTemplateKeys
{
    public const string TopupSuccess = "TopupSuccess";
    public const string AlaCarteOrderSuccess = "AlaCarteOrderSuccess";
    public const string MealComboOrderSuccess = "MealComboOrderSuccess";

    public static readonly IReadOnlyList<string> SystemKeys =
    [
        TopupSuccess,
        AlaCarteOrderSuccess,
        MealComboOrderSuccess
    ];
}
