using System.Globalization;
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

    /// <summary>Arbitration prompt, in the language the session speaks (§8.1).</summary>
    public static string Build(
        AdviceQuery query,
        IReadOnlyList<Opinion> opinions,
        LibraryIndex index,
        CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(opinions);

        var english = AnswerFormat.IsEnglish(culture);
        var format = AnswerFormat.ForCulture(culture);
        var builder = new StringBuilder(60_000);

        builder.AppendLine(english
            ? "You are the referee of a committee of rock/metal guitar advisors."
            : "Tu es l'arbitre d'un comité de conseillers guitare (rock/metal).");
        builder.AppendLine(english
            ? "Each of them gave an opinion on the same request and the same catalogue."
            : "Chacun a donné son avis sur la même demande et le même catalogue.");
        builder.AppendLine(english
            ? "Answer in English, direct, in 10 to 15 sentences."
            : "Réponds en français, ton direct, en 10 à 15 phrases.");
        builder.AppendLine();

        var demand = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Artist))
            demand.Add(english ? $"artist \"{query.Artist}\"" : $"artiste « {query.Artist} »");
        if (!string.IsNullOrWhiteSpace(query.Song))
            demand.Add(english ? $"song \"{query.Song}\"" : $"chanson « {query.Song} »");
        if (!string.IsNullOrWhiteSpace(query.Style))
            demand.Add(english ? $"style \"{query.Style}\"" : $"style « {query.Style} »");

        builder.AppendLine((english ? "The request: " : "La demande : ") + string.Join(", ", demand));

        builder.AppendLine();
        builder.Append(CataloguePrompt.DescribeCatalogue(index, culture));
        CataloguePrompt.AppendRules(builder, culture);

        builder.AppendLine();
        builder.AppendLine(english ? "The opinions received:" : "Les avis reçus :");
        foreach (var opinion in opinions)
        {
            if (string.IsNullOrWhiteSpace(opinion.Text))
                continue;

            builder.AppendLine($"--- {opinion.Provider} ---");
            builder.AppendLine(opinion.Text.Trim());
        }

        builder.AppendLine();
        builder.AppendLine(english
            ? "Your mission, in this exact format:"
            : "Ta mission, dans ce format exact :");
        builder.AppendLine(english
            ? $"  {format.Verdict} : the 3 best ranked proposals — for each: the block, the cabinet,"
            : $"  {format.Verdict} : les 3 meilleures propositions classées — pour chacune : le bloc, le baffle,");
        builder.AppendLine(english
            ? "            why, and the level of consensus (3/3, 2/3, 1/3). Challenge the opinions on the way:"
            : "            pourquoi, et le niveau de consensus (3/3, 2/3, 1/3). Confronte les avis au passage :");
        builder.AppendLine(english
            ? "            quote what does not add up for each one (a name absent from the catalogue, a stomp"
            : "            cite ce qui cloche chez chacun (un nom absent du catalogue, un stomp associé à un");
        builder.AppendLine(english
            ? "            paired with another block's amp, a cabinet that does not fit the style)."
            : "            ampli d'un autre bloc, un baffle qui ne va pas avec le style).");
        builder.AppendLine(english
            ? $"  {format.ConseilLibre} : what you would use yourself, with no library constraint — the real"
            : $"  {format.ConseilLibre} : ce que tu utiliserais toi, sans contrainte de bibliothèque — le matériel");
        builder.AppendLine(english
            ? "            gear of the track or the style if you know it, and the typical settings that go with it."
            : "            réel du morceau ou du style si tu le connais, et les réglages typiques qui vont avec.");
        builder.AppendLine();
        builder.AppendLine(english
            ? $"Start at \"{format.Verdict} :\", write nothing before it: no preamble, no analysis of the opinions."
            : $"Commence par « {format.Verdict} : », sans rien écrire avant : ni préambule, ni analyse des avis.");
        builder.AppendLine(english
            ? "Write the verdict directly: no list evaluation of the opinions, no draft, no " +
              "checking of your own format, no commentary on what you are about to answer."
            : "Écris le verdict directement : pas d'évaluation d'avis en liste, pas de brouillon, pas de " +
              "vérification de ton propre format, ni de commentaire sur ce que tu t'apprêtes à répondre.");
        builder.AppendLine(english
            ? "If every opinion is wrong, say so and decide with your own arguments: a unanimous " +
              "agreement is no proof."
            : "Si tous les avis se trompent, dis-le et tranche avec tes propres arguments : un accord " +
              "unanime n'est pas une preuve.");
        builder.AppendLine(english
            ? "Invent no block nor cabinet absent from the catalogue for the ranked proposals — the " +
              $"{format.ConseilLibre}, however, is open to everything you know."
            : "N'invente aucun bloc ni baffle absent du catalogue pour les propositions classées — le " +
              $"{format.ConseilLibre}, lui, est ouvert à tout ton savoir.");

        return builder.ToString();
    }
}
