using System.ComponentModel.DataAnnotations;

namespace SMS.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email address.")]
    [EmailAddress(ErrorMessage = "That is not a valid email address.")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
