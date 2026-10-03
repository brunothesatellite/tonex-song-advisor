using System.Globalization;
using System.Text;

namespace TonexAdvisor.Core.Data;

/// <summary>
/// Text normalisation shared by the search index and the scorer.
/// </summary>
/// <remarks>
/// Folders and preset names in TONEX libraries are riddled with accents, punctuation and casing
/// ("GalTone Studio - Guns N’ Roses"), so matching has to fold all of that away.
/// </remarks>
public static class Tokenizer
{
    /// <summary>Folds a string to lowercase, strips accents and collapses separators to spaces.</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var decomposition = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposition.Length);

        foreach (var character in decomposition)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(char.ToLowerInvariant(character));
            }
            else if (char.IsWhiteSpace(character) || char.IsPunctuation(character) || char.IsSymbol(character))
            {
                if (builder.Length > 0 && builder[^1] != ' ')
                    builder.Append(' ');
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>Normalizes <paramref name="value"/> and splits it into distinct tokens.</summary>
    public static List<string> Tokenize(string? value)
    {
        var normalized = Normalize(value);
        if (normalized.Length == 0)
            return new List<string>();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var tokens = new List<string>();

        foreach (var token in normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (seen.Add(token))
                tokens.Add(token);
        }

        return tokens;
    }

    /// <summary>True when <paramref name="haystack"/> contains <paramref name="needle"/>.</summary>
    public static bool Contains(string? haystack, string? needle)
    {
        var normalizedHaystack = Normalize(haystack);
        var normalizedNeedle = Normalize(needle);

        if (normalizedNeedle.Length == 0)
            return true;

        return normalizedHaystack.Contains(normalizedNeedle, StringComparison.Ordinal);
    }
}
