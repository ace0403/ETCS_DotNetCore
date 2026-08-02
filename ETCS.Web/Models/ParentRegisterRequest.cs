using System.ComponentModel.DataAnnotations;

namespace ETCS.Web.Models;

public sealed class ParentRegisterRequest
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(128)]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(128)]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username is required")]
    [StringLength(128)]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Enter a valid email address")]
    [StringLength(256)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mobile number is required")]
    [StringLength(40)]
    [Display(Name = "Mobile Number")]
    public string MobileNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [StringLength(128)]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm password is required")]
    [Compare(nameof(Password), ErrorMessage = "Confirm password does not match.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
