using System;
using System.Text.RegularExpressions;

namespace ETCS.Pos.Bridge.Services;

internal static class ReceiptTextNormalizer
{
    private static readonly Regex DashMojibakePattern = new(
        @"â€[\u0093\u0094""']|Ã¢â‚¬â€œ|Ã¢â‚¬â€\u009d",
        RegexOptions.Compiled);

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var text = value.Trim();
        text = DashMojibakePattern.Replace(text, "-");
        text = text
            .Replace('\u2013', '-')
            .Replace('\u2014', '-')
            .Replace('\u2212', '-')
            .Replace('\u00AD', '-');

        return text;
    }
}
