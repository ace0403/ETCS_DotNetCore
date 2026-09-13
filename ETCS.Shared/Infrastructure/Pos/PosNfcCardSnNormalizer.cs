using System.Globalization;
using System.Text;

namespace ETCS.Shared.Infrastructure.Pos;

public static class PosNfcCardSnNormalizer
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim())
        {
            if (character is ' ' or ':' or '-' or '.')
            {
                continue;
            }

            builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString();
    }

    public static IReadOnlyList<string> BuildCandidates(
        string? cardSn,
        string? uidHex,
        string? uidHexReversed,
        string? uidDecimal,
        string? uidDecimalReversed)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Add(candidates, cardSn);
        Add(candidates, uidHex);
        Add(candidates, uidHexReversed);
        Add(candidates, uidDecimal);
        Add(candidates, uidDecimalReversed);
        return candidates.ToList();
    }

    private static void Add(ISet<string> candidates, string? value)
    {
        var normalized = Normalize(value);
        if (normalized.Length == 0)
        {
            return;
        }

        candidates.Add(normalized);

        if (normalized.All(char.IsDigit)
            && normalized.Length > 1
            && normalized.StartsWith('0')
            && decimal.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric))
        {
            candidates.Add(numeric.ToString("0", CultureInfo.InvariantCulture));
        }
    }
}
