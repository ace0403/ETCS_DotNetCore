namespace ETCS.Web.Models;

public sealed class AlaCarteSelectedItemRequest
{
    public int ItemId { get; set; }

    public string MealDate { get; set; } = string.Empty;

    public Guid Id { get; set; }
}
