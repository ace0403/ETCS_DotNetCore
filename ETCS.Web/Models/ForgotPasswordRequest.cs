using System.ComponentModel.DataAnnotations;

namespace ETCS.Web.Models;

public sealed class ForgotPasswordRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}
