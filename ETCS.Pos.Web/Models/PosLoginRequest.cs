using System.ComponentModel.DataAnnotations;

namespace ETCS.Pos.Web.Models;

public sealed class PosLoginRequest
{
    [Required(ErrorMessage = "Username is required")]
    [MaxLength(100)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}
