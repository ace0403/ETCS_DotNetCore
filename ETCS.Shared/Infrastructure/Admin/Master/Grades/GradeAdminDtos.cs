using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Admin.Master.Grades;

public sealed class GradeListItemDto
{
    public int Id { get; init; }
    public string Grade { get; init; } = string.Empty;
}

public sealed class GradeSaveRequest
{
    public int Id { get; set; }

    [Display(Name = "Grade")]
    [Required(ErrorMessage = "Grade is required.")]
    [MaxLength(50)]
    public string Grade { get; set; } = string.Empty;
}
