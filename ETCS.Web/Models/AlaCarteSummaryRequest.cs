namespace ETCS.Web.Models;

public sealed class AlaCarteSummaryRequest
{
    public int StudentId { get; set; }

    public List<AlaCarteSelectedItemRequest> Items { get; set; } = [];
}
