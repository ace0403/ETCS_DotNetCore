namespace ETCS.Web.Models;

public sealed class AccountSettingsPageViewModel
{
    public string DisplayName { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string MaskedEmail { get; init; } = string.Empty;
}
