using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.App.ViewModels;

/// <summary>
/// One line of an answer, split so the format markers can be coloured: <c>BLOC :</c>/<c>BLOCK :</c>,
/// <c>BAFFLE :</c>/<c>CAB :</c>, <c>RÉGLAGES :</c>/<c>SETTINGS :</c>, <c>ALTERNATIVE :</c>,
/// <c>CONSEIL LIBRE :</c>/<c>FREE ADVICE :</c>, <c>VERDICT :</c> and the numbered proposals of a
/// verdict. Both languages are painted whatever the session speaks: a stray English marker must
/// not lose its colour in a French answer, nor the reverse.
/// </summary>
public sealed class AnswerLineViewModel
{
    private static readonly string[] Markers = [.. AnswerFormat.AllMarkers];

    public AnswerLineViewModel(string label, string body)
    {
        Label = label;
        Body = body;
    }

    /// <summary>The coloured part: the marker, or the number of a proposal.</summary>
    public string Label { get; }

    /// <summary>The rest of the line.</summary>
    public string Body { get; }

    /// <summary>The lines of an answer, ready to render with their coloured markers.</summary>
    public static IReadOnlyList<AnswerLineViewModel> Split(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<AnswerLineViewModel>();

        return text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .Select(Line)
            .ToList();
    }

    private static AnswerLineViewModel Line(string line)
    {
        var text = Unwrap(line);

        foreach (var marker in Markers)
        {
            // Le libellé et son deux-points sont tolérés avec ou sans espace entre les deux :
            // « BAFFLE : », « BAFFLE: » et « BAFFLE  : » sont le même repère.
            var key = marker[..^1].TrimEnd();
            if (!text.StartsWith(key, StringComparison.OrdinalIgnoreCase))
                continue;

            var after = text[key.Length..].TrimStart(' ', '\t');
            if (!after.StartsWith(':'))
                continue;

            return new AnswerLineViewModel(marker, after[1..].TrimStart(' ', '\t', '-'));
        }

        // « 1. » d'un verdict : numéroté, donc à colorier lui aussi.
        var dot = text.IndexOf(". ", StringComparison.Ordinal);
        if (dot is > 0 and < 4 && text.Take(dot).All(char.IsDigit))
            return new AnswerLineViewModel(text[..(dot + 1)], text[(dot + 2)..]);

        return new AnswerLineViewModel("", text);
    }

    /// <summary>
    /// A marker hidden behind a bullet, a bold, an indent or a non-breaking space must still be
    /// found: models wrap their labels in all of those.
    /// </summary>
    private static string Unwrap(string line)
    {
        // L'indentation ne doit pas cacher le libellé.
        var text = line.TrimStart(' ', '\t', '\u00A0');

        // Une puce de liste devant le libellé. L'astérisque ne compte que suivi d'un espace,
        // pour ne pas confondre la puce avec le gras.
        if (text.Length > 0 && ("-•–>".Contains(text[0]) || text.StartsWith("* ", StringComparison.Ordinal)))
            text = text[1..].TrimStart(' ', '\t', '\u00A0');

        // Le gras autour du libellé : « **BAFFLE** : ».
        if (text.StartsWith("**", StringComparison.Ordinal))
        {
            var close = text.IndexOf("**", 2, StringComparison.Ordinal);
            if (close > 0)
                text = text[2..close] + text[(close + 2)..].TrimStart(' ', '\t', '\u00A0');
        }

        return text.Replace('\u00A0', ' ');
    }
}