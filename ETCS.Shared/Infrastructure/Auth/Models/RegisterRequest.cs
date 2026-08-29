using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Auth.Models;

public sealed class RegisterRequest
{
    [Required]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    public string LastName { get; init; } = string.Empty;

    [Required]
    public string Username { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string MobileNumber { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Short-lived token from POST api/Auth/verify-otp. Required by the API register endpoint.
    /// </summary>
    public string? VerificationToken { get; init; }
}
