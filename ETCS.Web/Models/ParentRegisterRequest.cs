using System.ComponentModel.DataAnnotations;

namespace ETCS.Web.Models;

public sealed class ParentRegisterRequest
{
    [Required(ErrorMessage = "First name is required")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    /// <summary>Stored as Email; not shown on the registration UI.</summary>
    public string Username => Email;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required")]
    [Display(Name = "Mobile Number")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required")]
    [Compare(nameof(Password), ErrorMessage = "Confirm password does not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Filled after successful OTP verify; required to complete registration (enforced in Register action).</summary>
    [Display(Name = "Verification Token")]
    public string VerificationToken { get; set; } = string.Empty;
}
