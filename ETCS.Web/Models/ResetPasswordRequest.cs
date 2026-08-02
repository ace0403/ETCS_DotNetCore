using System.ComponentModel.DataAnnotations;

namespace ETCS.Web.Models;

public sealed class ResetPasswordRequest
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "New password is required")]
    [MinLength(8, ErrorMessage = "New password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required")]
    [Compare(nameof(NewPassword), ErrorMessage = "Confirm password does not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
