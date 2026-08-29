namespace ETCS.Web.Infrastructure.Navigation;

public sealed class ParentPortalNavigationAccess
{
    public static ParentPortalNavigationAccess None { get; } = new();

    public bool ShowWallet { get; init; }

    public bool ShowPreOrderMeal { get; init; }
}
