namespace TonexAdvisor.Core.Advice;

/// <summary>
/// A reasoning model writes its thinking before its answer, sometimes right inside the answer:
/// the panel shows the verdict, not the working.
/// </summary>
/// <remarks>
/// Rather than guessing where the answer starts, we lean on the format the prompts impose: an
/// answer begins at its marker (<c>BLOC :</c> for a voice, <c>VERDICT :</c> for the arbitration).
/// A model that does not follow the format is shown as it wrote it — cutting blindly would be
/// worse than a long answer.
/// </remarks>
public static class AnswerCleaner
{
    /// <summary>Keeps what starts at the first marker; whatever comes before is thinking.</summary>
    public static string TrimTo(string? text, string marker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(marker);

        if (string.IsNullOrWhiteSpace(text))
            return "";

        var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index > 0 ? text[index..].TrimStart() : text.Trim();
    }
}
