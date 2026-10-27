using System.ComponentModel.DataAnnotations;

namespace SMS.Web.Models;

public class SetupViewModel
{
    [Required(ErrorMessage = "Enter the school's name.")]
    [StringLength(200)]
    [Display(Name = "School name")]
    public string SchoolName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the branch name.")]
    [StringLength(200)]
    [Display(Name = "Branch name")]
    public string BranchName { get; set; } = "Main";

    [Required(ErrorMessage = "Choose a time zone.")]
    [StringLength(64)]
    [Display(Name = "Time zone")]
    public string TimeZone { get; set; } = "Asia/Karachi";

    [Required(ErrorMessage = "Enter the administrator's name.")]
    [StringLength(200)]
    [Display(Name = "Administrator name")]
    public string AdminDisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter an email address.")]
    [EmailAddress(ErrorMessage = "That is not a valid email address.")]
    [StringLength(256)]
    [Display(Name = "Administrator email")]
    public string AdminEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "The two passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
