namespace ETCS.Shared.Application.Email;

public static class EmailTemplateKeys
{
    public const string TopupSuccess = "TopupSuccess";
    public const string AlaCarteOrderSuccess = "AlaCarteOrderSuccess";
    public const string MealComboOrderSuccess = "MealComboOrderSuccess";
    public const string PasswordReset = "PasswordReset";
    public const string RegistrationOtp = "RegistrationOtp";
    public const string RegistrationSuccess = "RegistrationSuccess";
    public const string DeleteAccountOtp = "DeleteAccountOtp";
    public const string ReplaceCardRequest = "ReplaceCardRequest";

    public static readonly IReadOnlyList<string> SystemKeys =
    [
        TopupSuccess,
        AlaCarteOrderSuccess,
        MealComboOrderSuccess,
        PasswordReset,
        RegistrationOtp,
        RegistrationSuccess,
        DeleteAccountOtp,
        ReplaceCardRequest
    ];
}
