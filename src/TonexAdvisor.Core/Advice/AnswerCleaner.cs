using System.Text;

namespace TonexAdvisor.Core.Advice;

/// <summary>
/// A reasoning model writes its working before its answer — drafts, self-checks, sentence counts,
/// sometimes after the answer too. The panel shows the conclusion, not the working.
/// </summary>
/// <remarks>
/// Rather than guessing what is thinking and what is answer, we lean on the format the prompts
/// impose: the answer is the block that starts at its marker (<c>BLOC :</c> for a voice,
/// <c>VERDICT :</c> for the arbitration) and ends with the <c>CONSEIL LIBRE</c> paragraph. A
/// model that repeats its answer three times while polishing it is therefore reduced to the last
/// one — and a model that ignores the format entirely is shown as it wrote it, because cutting
/// blindly would be worse than a long answer.
/// </remarks>
public static class AnswerCleaner
{
    /// <summary>
    /// Keeps the answer block: from the <b>last</b> line starting at <paramref name="startMarker"/>
    /// to the end of the <paramref name="endMarker"/> paragraph.
    /// </summary>
    public static string Extract(string? text, string startMarker, string endMarker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startMarker);
        ArgumentException.ThrowIfNullOrWhiteSpace(endMarker);

        if (string.IsNullOrWhiteSpace(text))
            return "";

        var lines = text.Replace("\r\n", "\n").Split('\n');

        // Dernier bloc : les répétitions qui précèdent sont des brouillons.
        var start = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith(startMarker, StringComparison.OrdinalIgnoreCase))
                start = i;
        }

        if (start < 0)
            return text.Trim();

        // Fin : après le paragraphe qui porte la fin du format, tout le reste est du travail.
        var end = lines.Length;
        var seenEndMarker = false;

        for (var i = start; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith(endMarker, StringComparison.OrdinalIgnoreCase))
            {
                seenEndMarker = true;
                continue;
            }

            if (seenEndMarker && string.IsNullOrWhiteSpace(lines[i]))
            {
                end = i;
                break;
            }
        }

        var answer = string.Join("\n", lines[start..end]).Trim();

        // Une réponse coupée en plein milieu par la limite de tokens se remarque d'un coup d'œil.
        return MarkIfTruncated(answer);
    }

    /// <summary>Adds a visible ellipsis to an answer the model never finished writing.</summary>
    private static string MarkIfTruncated(string answer)
    {
        if (answer.Length == 0)
            return answer;

        return answer[^1] is '.' or '!' or '?' or ':' or '»' or '"' or '\''
            ? answer
            : answer + " …";
    }
}
