using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Auth.Models;

public sealed class SendOtpRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;
}
