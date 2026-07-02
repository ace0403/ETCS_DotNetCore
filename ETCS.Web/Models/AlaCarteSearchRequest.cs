using System.ComponentModel.DataAnnotations;

namespace ETCS.Web.Models;

public sealed class AlaCarteSearchRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int StudentId { get; set; }

    [Required]
    public DateTime MealDate { get; set; }
}
