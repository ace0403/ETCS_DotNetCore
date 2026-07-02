using System.ComponentModel.DataAnnotations;

namespace ETCS.Web.Models;

public sealed class AlaCartePlaceOrderRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int StudentId { get; set; }

    public List<AlaCarteSelectedItemRequest> MealList { get; set; } = [];
}
