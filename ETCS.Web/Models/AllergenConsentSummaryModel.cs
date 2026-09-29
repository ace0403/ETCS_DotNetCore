namespace ETCS.Web.Models;

public sealed class AllergenConsentSummaryModel
{
    public string ChildLabel { get; init; } = "your child";

    public string ItemKind { get; init; } = "meal";

    public string? AllergenItemText { get; init; }
}
