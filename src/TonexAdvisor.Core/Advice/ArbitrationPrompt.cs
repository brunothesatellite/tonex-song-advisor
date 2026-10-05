using System.Globalization;
using System.Text;
using TonexAdvisor.Core.Data;
using TonexAdvisor.Core.Localization;

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

        var format = AnswerFormat.ForCulture(culture);
        var builder = new StringBuilder(60_000);

        builder.AppendLine(CoreTexts.Get("Invite.Arbitre.Role", culture));
        builder.AppendLine(CoreTexts.Get("Invite.Arbitre.Role.Suite", culture));
        builder.AppendLine(CoreTexts.Get("Invite.Arbitre.Langue", culture));
        builder.AppendLine();

        var demand = new List<string>();
        if (!string.IsNullOrWhiteSpace(query.Artist))
            demand.Add(CoreTexts.Format("Invite.Arbitre.Requete.Artiste", culture, query.Artist));
        if (!string.IsNullOrWhiteSpace(query.Song))
            demand.Add(CoreTexts.Format("Invite.Arbitre.Requete.Chanson", culture, query.Song));
        if (!string.IsNullOrWhiteSpace(query.Style))
            demand.Add(CoreTexts.Format("Invite.Arbitre.Requete.Style", culture, query.Style));

        builder.AppendLine(CoreTexts.Get("Invite.Arbitre.Requete.Titre", culture) + string.Join(", ", demand));

        builder.AppendLine();
        builder.Append(CataloguePrompt.DescribeCatalogue(index, culture));
        CataloguePrompt.AppendRules(builder, culture);

        builder.AppendLine();
        builder.AppendLine(CoreTexts.Get("Invite.Arbitre.Avis", culture));
        foreach (var opinion in opinions)
        {
            if (string.IsNullOrWhiteSpace(opinion.Text))
                continue;

            builder.AppendLine($"--- {opinion.Provider} ---");
            builder.AppendLine(opinion.Text.Trim());
        }

        builder.AppendLine();
        builder.AppendLine(CoreTexts.Get("Invite.Arbitre.Mission", culture));
        builder.AppendLine(CoreTexts.Format("Invite.Arbitre.Format.Verdict", culture, format.Verdict));
        builder.AppendLine(CoreTexts.Get("Invite.Arbitre.Format.VerdictSuite", culture));
        builder.AppendLine(CoreTexts.Get("Invite.Arbitre.Format.VerdictPreuves", culture));
        builder.AppendLine(CoreTexts.Get("Invite.Arbitre.Format.VerdictPreuvesSuite", culture));
        builder.AppendLine(CoreTexts.Format("Invite.Arbitre.Format.Libre", culture, format.ConseilLibre));
        builder.AppendLine(CoreTexts.Get("Invite.Arbitre.Format.LibreSuite", culture));
        builder.AppendLine();
        builder.AppendLine(CoreTexts.Format("Invite.Arbitre.Format.Ouverture", culture, format.Verdict));
        builder.AppendLine(CoreTexts.Get("Invite.Arbitre.Regles.Style", culture));
        builder.AppendLine(CoreTexts.Get("Invite.Arbitre.Regles.Consensus", culture));
        builder.AppendLine(CoreTexts.Format("Invite.Arbitre.Regles.Catalogue", culture, format.ConseilLibre));

        return builder.ToString();
    }
}
