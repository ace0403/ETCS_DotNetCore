using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Auth.Models;

public sealed class VerifyOtpRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    public string Otp { get; init; } = string.Empty;
}
