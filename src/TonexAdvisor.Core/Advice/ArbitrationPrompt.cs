using System.Text;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Advice;

/// <param name="Provider">Which voice said it.</param>
/// <param name="Text">What it said.</param>
public sealed record Opinion(string Provider, string Text);

/// <summary>
/// Builds the arbitration prompt: the referee hears every voice and must defend none of them.
/// </summary>
/// <remarks>
/// Gemini, Mistral and Groq are asked to <b>contradict</b>, not to agree — a panel where
/// everyone nods is worth one voice. The referee then keeps the three best proposals, each with
/// its level of consensus, and never leaves the local shortlist.
/// </remarks>
public static class ArbitrationPrompt
{
    /// <summary>The referee quotes several voices and ranks three proposals.</summary>
    public const int MaxTokens = 4000;

    public static string Build(
        AdviceQuery query,
        IReadOnlyList<ScoredPreset> presets,
        ScoredCombination? combination,
        IReadOnlyList<Opinion> opinions,
        LibraryIndex? index = null)
    {
        ArgumentNullException.ThrowIfNull(opinions);

        var builder = new StringBuilder(3200);

        builder.AppendLine("Tu es l'arbitre d'un comité de conseillers guitare (rock/metal).");
        builder.AppendLine("Chacun a donné son avis sur la même demande et la même sélection locale.");
        builder.AppendLine("Réponds en français, ton direct, en 10 à 15 phrases.");
        builder.AppendLine();

        builder.Append(AdvicePrompt.DescribeShortlist(query, presets, combination, index));
        AdvicePrompt.AppendRules(builder);
        builder.AppendLine();

        builder.AppendLine("Les avis reçus :");
        foreach (var opinion in opinions)
        {
            if (string.IsNullOrWhiteSpace(opinion.Text))
                continue;

            builder.AppendLine($"--- {opinion.Provider} ---");
            builder.AppendLine(opinion.Text.Trim());
        }

        builder.AppendLine();
        builder.AppendLine("Ta mission :");
        builder.AppendLine(
            "1. Confronte les avis : cite ce qui cloche chez chacun (un preset absent de la liste, " +
            "un stomp associé à un ampli d'une autre capture, un réglage contredit par les valeurs).");
        builder.AppendLine(
            "2. Classe les 3 meilleures propositions, uniquement issues de la sélection ci-dessus. " +
            "Pour chacune : le preset ou le bloc, pourquoi, et le niveau de consensus (3/3, 2/3, 1/3).");
        builder.AppendLine(
            "3. Termine par 2 à 3 réglages concrets (gain, EQ, réverb/delay) et une alternative.");
        builder.AppendLine(
            "Si tous les avis se trompent, dis-le et tranche avec tes propres arguments : un accord " +
            "unanime n'est pas une preuve.");
        builder.AppendLine("N'invente aucun preset, ampli ou baffle hors sélection.");

        return builder.ToString();
    }
}
