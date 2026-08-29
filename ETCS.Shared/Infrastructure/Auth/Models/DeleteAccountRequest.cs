namespace ETCS.Shared.Infrastructure.Auth.Models;

public sealed class DeleteAccountRequest
{
    public string Otp { get; set; } = string.Empty;
}
