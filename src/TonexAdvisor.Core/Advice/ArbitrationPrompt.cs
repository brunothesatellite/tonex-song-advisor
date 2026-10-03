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
/// Gemini, Mistral and Groq are asked to <b>contradict</b>, not to agree — a panel where everyone
/// nods is worth one voice. The referee then keeps the three best proposals, each with its level
/// of consensus, and never leaves the catalogue.
/// </remarks>
public static class ArbitrationPrompt
{
    /// <summary>
    /// The referee quotes several voices, ranks three proposals and adds its free advice: its
    /// answer is the longest of the panel, and its thinking is billed in the same budget. Cut it
    /// short and the verdict stops mid-sentence.
    /// </summary>
    public const int MaxTokens = 8000;

    public static string Build(
        AdviceQuery query,
        IReadOnlyList<Opinion> opinions,
        LibraryIndex index)
    {
        ArgumentNullException.ThrowIfNull(opinions);

        var builder = new StringBuilder(60_000);

        builder.AppendLine("Tu es l'arbitre d'un comité de conseillers guitare (rock/metal).");
        builder.AppendLine("Chacun a donné son avis sur la même demande et le même catalogue.");
        builder.AppendLine("Réponds en français, ton direct, en 10 à 15 phrases.");
        builder.AppendLine();

        var demand = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Artist))
            demand.Add($"artiste « {query.Artist} »");
        if (!string.IsNullOrWhiteSpace(query.Song))
            demand.Add($"chanson « {query.Song} »");
        if (!string.IsNullOrWhiteSpace(query.Style))
            demand.Add($"style « {query.Style} »");

        builder.AppendLine("La demande : " + string.Join(", ", demand));

        builder.AppendLine();
        builder.Append(CataloguePrompt.DescribeCatalogue(index));
        CataloguePrompt.AppendRules(builder);

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
        builder.AppendLine("Ta mission, dans ce format exact :");
        builder.AppendLine("  VERDICT : les 3 meilleures propositions classées — pour chacune : le bloc, le baffle,");
        builder.AppendLine("            pourquoi, et le niveau de consensus (3/3, 2/3, 1/3). Confronte les avis au passage :");
        builder.AppendLine("            cite ce qui cloche chez chacun (un nom absent du catalogue, un stomp associé à un");
        builder.AppendLine("            ampli d'un autre bloc, un baffle qui ne va pas avec le style).");
        builder.AppendLine("  CONSEIL LIBRE : ce que tu utiliserais toi, sans contrainte de bibliothèque — le matériel");
        builder.AppendLine("            réel du morceau ou du style si tu le connais, et les réglages typiques qui vont avec.");
        builder.AppendLine();
        builder.AppendLine("Commence par « VERDICT : », sans rien écrire avant : ni préambule, ni analyse des avis.");
        builder.AppendLine(
            "Écris le verdict directement : pas d'évaluation d'avis en liste, pas de brouillon, pas de " +
            "vérification de ton propre format, ni de commentaire sur ce que tu t'apprêtes à répondre.");
        builder.AppendLine(
            "Si tous les avis se trompent, dis-le et tranche avec tes propres arguments : un accord " +
            "unanime n'est pas une preuve.");
        builder.AppendLine(
            "N'invente aucun bloc ni baffle absent du catalogue pour les propositions classées — le " +
            "CONSEIL LIBRE, lui, est ouvert à tout ton savoir.");

        return builder.ToString();
    }
}
