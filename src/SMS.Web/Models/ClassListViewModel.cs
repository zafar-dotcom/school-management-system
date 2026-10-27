using System.ComponentModel.DataAnnotations;
using SMS.Domain.SchoolData;

namespace SMS.Web.Models;

public class ClassListViewModel
{
    public IReadOnlyList<SchoolClass> Classes { get; init; } = [];

    public string SchoolName { get; init; } = string.Empty;
    public string BranchName { get; init; } = string.Empty;

    /// <summary>Shown so it is visible that the filter is real, not that the table is empty.</summary>
    public Guid ActiveBranchId { get; init; }

    public NewClassViewModel NewClass { get; init; } = new();
}

public class NewClassViewModel
{
    [Required(ErrorMessage = "Enter a code, e.g. G1.")]
    [StringLength(20)]
    [Display(Name = "Code")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a name, e.g. Grade 1.")]
    [StringLength(200)]
    [Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [Range(0, 999)]
    [Display(Name = "Sort order")]
    public int SortOrder { get; set; }
}
