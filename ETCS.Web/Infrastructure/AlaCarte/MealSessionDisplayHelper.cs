namespace ETCS.Web.Infrastructure.AlaCarte;

public static class MealSessionDisplayHelper
{
    public static string ResolveIcon(string? sessionName) => AlaCarteMealTypeHelper.ResolveIcon(sessionName);

    public static string ResolveSubtitle(string? sessionName)
    {
        var name = sessionName?.Trim() ?? string.Empty;
        if (name.Contains("breakfast", StringComparison.OrdinalIgnoreCase))
        {
            return "Snacks, Sandwiches, Dairy & Drinks";
        }

        if (name.Contains("lunch", StringComparison.OrdinalIgnoreCase))
        {
            return "Meal Packages, Dairy & Drinks";
        }

        if (name.Contains("dinner", StringComparison.OrdinalIgnoreCase)
            || name.Contains("supper", StringComparison.OrdinalIgnoreCase))
        {
            return "Meal Packages & Drinks";
        }

        return "Meal packages and add-ons";
    }
}
