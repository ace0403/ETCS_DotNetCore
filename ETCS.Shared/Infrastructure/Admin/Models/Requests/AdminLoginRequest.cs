using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Admin.Models.Requests;

public sealed class AdminLoginRequest
{
    [Required(ErrorMessage = "Username is required")]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
