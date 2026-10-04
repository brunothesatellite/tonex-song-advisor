namespace TonexAdvisor.App.ViewModels;

/// <summary>
/// One line of an answer, split so the format markers can be coloured: <c>BLOC :</c>,
/// <c>BAFFLE :</c>, <c>RÉGLAGES :</c>, <c>ALTERNATIVE :</c>, <c>CONSEIL LIBRE :</c>,
/// <c>VERDICT :</c> and the numbered proposals of a verdict.
/// </summary>
public sealed class AnswerLineViewModel
{
    private static readonly string[] Markers =
    [
        "BLOC :", "BAFFLE :", "RÉGLAGES :", "ALTERNATIVE :", "CONSEIL LIBRE :", "VERDICT :",
    ];

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
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => line.Length > 0)
            .Select(Line)
            .ToList();
    }

    private static AnswerLineViewModel Line(string line)
    {
        foreach (var marker in Markers)
        {
            if (line.StartsWith(marker, StringComparison.OrdinalIgnoreCase))
                return new AnswerLineViewModel(marker, line[marker.Length..].TrimStart());
        }

        // « 1. » d'un verdict : numéroté, donc à colorier lui aussi.
        var dot = line.IndexOf(". ", StringComparison.Ordinal);
        if (dot is > 0 and < 4 && line.Take(dot).All(char.IsDigit))
            return new AnswerLineViewModel(line[..(dot + 1)], line[(dot + 2)..]);

        return new AnswerLineViewModel("", line);
    }
}
