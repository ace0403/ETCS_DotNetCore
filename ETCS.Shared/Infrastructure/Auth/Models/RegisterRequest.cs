using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Auth.Models;

public sealed class RegisterRequest
{
    [Required]
    [StringLength(128)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [StringLength(128)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string MobileNumber { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [StringLength(128)]
    public string Password { get; init; } = string.Empty;
}
