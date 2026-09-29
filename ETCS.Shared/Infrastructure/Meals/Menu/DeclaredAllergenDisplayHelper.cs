namespace ETCS.Shared.Infrastructure.Meals.Menu;

/// <summary>
/// Shared allergen chip tone/icon resolution for portal menu and POS tiles.
/// </summary>
public static class DeclaredAllergenDisplayHelper
{
    public static string? NormalizeIconFileName(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return null;
        }

        var fileName = Path.GetFileName(icon.Trim());
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        return fileName;
    }

    public static string? TryGetIconFileFromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();
        if (!LooksLikeIconFileName(trimmed))
        {
            return null;
        }

        return NormalizeIconFileName(trimmed);
    }

    public static string ResolveToneClass(string allergenName)
    {
        var name = allergenName ?? string.Empty;
        if (Contains(name, "milk")) return "is-milk";
        if (Contains(name, "butter")) return "is-butter";
        if (Contains(name, "cheese") || Contains(name, "dairy")) return "is-dairy";
        if (Contains(name, "nut") || Contains(name, "peanut") || Contains(name, "almond")) return "is-nut";
        if (Contains(name, "egg")) return "is-egg";
        if (Contains(name, "wheat") || Contains(name, "gluten") || Contains(name, "bread")) return "is-gluten";
        if (Contains(name, "fish") || Contains(name, "seafood") || Contains(name, "shellfish")) return "is-fish";
        if (Contains(name, "soy") || Contains(name, "soya")) return "is-soy";
        return "is-default";
    }

    public static string ResolveTablerIconClass(string allergenName)
    {
        var name = allergenName ?? string.Empty;
        if (Contains(name, "milk")) return "ti ti-bottle";
        if (Contains(name, "butter")) return "ti ti-droplet";
        if (Contains(name, "cheese") || Contains(name, "dairy")) return "ti ti-cheese";
        if (Contains(name, "nut") || Contains(name, "peanut") || Contains(name, "almond")) return "ti ti-nut";
        if (Contains(name, "egg")) return "ti ti-egg";
        if (Contains(name, "wheat") || Contains(name, "gluten") || Contains(name, "bread")) return "ti ti-bread";
        if (Contains(name, "fish") || Contains(name, "seafood") || Contains(name, "shellfish")) return "ti ti-fish";
        if (Contains(name, "soy") || Contains(name, "soya")) return "ti ti-leaf";
        if (Contains(name, "sesame")) return "ti ti-grain";
        return "ti ti-alert-circle";
    }

    private static bool LooksLikeIconFileName(string value)
    {
        if (value.Length > 120 || value.Contains(' ', StringComparison.Ordinal))
        {
            return false;
        }

        return value.Contains('.', StringComparison.Ordinal)
               && (value.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                   || value.EndsWith(".svg", StringComparison.OrdinalIgnoreCase)
                   || value.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                   || value.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                   || value.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)
                   || value.EndsWith(".gif", StringComparison.OrdinalIgnoreCase));
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
