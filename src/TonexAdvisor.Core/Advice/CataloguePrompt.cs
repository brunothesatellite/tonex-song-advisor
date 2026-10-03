using System.Text;
using System.Text.RegularExpressions;
using TonexAdvisor.Core.Data;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Advice;

/// <summary>
/// Builds the prompt sent to the AIs: the request plus the catalogue of what the library holds,
/// names only.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately richer than the local shortlist: the local ranking matches words, the AIs match
/// meaning — « un son Slash » finds « AFD100 » here and never would in a token match.
/// </para>
/// <para>
/// Names only, and <b>selected</b>: measured on a 3 097 models library, all 894 blocks and 557
/// cabinets made 9 600 tokens, which free tiers refuse (Groq caps at 8 000 tokens per minute).
/// The catalogue therefore carries the most used amplifiers and cabinets first — the captures
/// that already serve the user's presets — and stops at <see cref="MaxAmps"/> and
/// <see cref="MaxCabs"/>. Blocks stay paired: each amplifier lists the stomps captured with it,
/// because mixing a stomp of one capture with an amplifier of another is exactly what TONEX
/// refuses.
/// </para>
/// </remarks>
public static class CataloguePrompt
{
    /// <summary>
    /// Answer budget. The imposed answer is four lines, but a reasoning model spends most of its
    /// budget before writing the first word — cutting it short returns the thinking instead.
    /// </summary>
    public const int MaxTokens = 3000;

    /// <summary>Amplifiers sent to the AIs, most used first.</summary>
    public const int MaxAmps = 200;

    /// <summary>Cabinets sent to the AIs, most used first.</summary>
    public const int MaxCabs = 120;

    /// <summary>Capture names like « 78 Signal Chain » or « 121 » tell the AIs nothing.</summary>
    private static readonly Regex PureNumber = new(@"^\d+$", RegexOptions.Compiled);

    private static readonly Regex SignalChainNoise = new(@"^\d+ Signal Chain$", RegexOptions.Compiled);

    /// <summary>French prompt for one voice.</summary>
    public static string Build(AdviceQuery query, LibraryIndex index)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(index);

        var builder = new StringBuilder(46_000);

        builder.AppendLine("Tu es un conseiller guitare (rock/metal) qui connaît les bibliothèques TONEX.");
        builder.AppendLine("Réponds en français, en 8 à 12 phrases, ton direct et concret.");
        builder.AppendLine("Va droit au but : ni préambule, ni analyse de la demande.");
        builder.AppendLine(
            "Écris uniquement les lignes du format ci-dessous : ni brouillon, ni vérification de ton " +
            "propre format, ni décompte de phrases, ni commentaire sur ce que tu t'apprêtes à répondre.");
        builder.AppendLine();

        builder.Append("La demande :");
        AppendField(builder, "artiste", query.Artist);
        AppendField(builder, "chanson", query.Song);
        AppendField(builder, "style", query.Style);
        builder.AppendLine();

        builder.AppendLine();
        builder.Append(DescribeCatalogue(index));
        AppendRules(builder);

        return builder.ToString();
    }

    /// <summary>
    /// The catalogue itself — the captured blocks and the cabinets, names only, most used first.
    /// Shared with the arbitration so every voice works on the same material.
    /// </summary>
    public static string DescribeCatalogue(LibraryIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);

        var builder = new StringBuilder(18_000);

        builder.AppendLine("Blocs capturés disponibles — stomp + ampli indissociables :");
        builder.AppendLine("Chaque ligne = un ampli, avec les stomp capturés avec lui. Un bloc = un ampli + au plus un de ses stomp.");

        foreach (var line in RankAmps(index).Take(MaxAmps))
            builder.AppendLine(line);

        builder.AppendLine();
        builder.AppendLine("Baffles disponibles — interchangeables :");

        foreach (var cabinet in RankCabs(index).Take(MaxCabs))
            builder.AppendLine(cabinet);

        builder.AppendLine();
        return builder.ToString();
    }

    /// <summary>Amplifiers with their captured stomps, most used first.</summary>
    private static IEnumerable<string> RankAmps(LibraryIndex index)
        => index.ToneModels
            .Where(model => model.AmpName.Length > 0 && !IsNoise(model.AmpName))
            .GroupBy(model => Tokenizer.Normalize(model.AmpName))
            .Select(group =>
            {
                var stomps = group
                    .Select(model => model.StompName)
                    .Where(stomp => stomp.Length > 0 && !IsNoise(stomp))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(stomp => stomp, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var name = group.First().AmpName;
                var line = stomps.Count > 0
                    ? $"{name} : {string.Join(" | ", stomps)}"
                    : $"{name} : (sans stomp)";

                return (Line: line, Usage: group.Sum(model => index.PresetsFor(model.Key).Count));
            })
            .OrderByDescending(item => item.Usage)
            .ThenBy(item => item.Line, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Line);

    /// <summary>Cabinets, most used first.</summary>
    private static IEnumerable<string> RankCabs(LibraryIndex index)
        => index.ToneModels
            .Where(model => model.CabName.Length > 0 && !IsNoise(model.CabName))
            .GroupBy(model => model.CabName, StringComparer.OrdinalIgnoreCase)
            .Select(group => (
                Name: group.Key,
                Usage: group.Sum(model => index.PresetsFor(model.Key).Count)))
            .OrderByDescending(item => item.Usage)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(item => item.Name);

    /// <summary>Capture names that say nothing to a human, nor to a model.</summary>
    private static bool IsNoise(string name)
    {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return false;

        return trimmed.Length <= 2
               || PureNumber.IsMatch(trimmed)
               || SignalChainNoise.IsMatch(trimmed);
    }

    /// <summary>The constraint every answer has to respect, and the format it must answer in.</summary>
    public static void AppendRules(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AppendLine("Règles :");
        builder.AppendLine(
            "- Un bloc = un ampli de la liste + au plus un des stomp capturés avec lui. Ne mélange " +
            "jamais un stomp d'un autre ampli : ils ont été capturés ensemble.");
        builder.AppendLine("- Le baffle est libre : choisis celui qui sert le mieux le style, dans la liste.");
        builder.AppendLine("- Réponds exactement dans ce format, en reprenant les noms tels quels :");
        builder.AppendLine("  BLOC : <stomp> -> <ampli>, ou <ampli> s'il n'y a pas de stomp");
        builder.AppendLine("  BAFFLE : <nom du baffle choisi>");
        builder.AppendLine("  RÉGLAGES : 2 à 3 réglages concrets (gain, EQ, réverb/delay)");
        builder.AppendLine("  ALTERNATIVE : <un autre bloc>");
        builder.AppendLine(
            "  CONSEIL LIBRE : ce que tu utiliserais toi, sans aucune contrainte de bibliothèque — " +
            "le matériel réel du morceau ou du style si tu le connais (ampli, baffle, pédales, " +
            "accordage), et les réglages typiques qui vont avec.");
        builder.AppendLine(
            "- N'invente aucun nom absent des listes ci-dessus : cette contrainte ne vaut que pour " +
            "BLOC, BAFFLE et ALTERNATIVE. CONSEIL LIBRE, lui, est ouvert à tout ton savoir.");
    }

    /// <summary>
    /// A block reads <c>stomp -&gt; ampli</c>, or the amplifier alone when the capture has no
    /// stomp. Exactly what the answer must name back.
    /// </summary>
    public static string BlockName(ToneModelRecord model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.AmpName.Length == 0)
            return "";

        return model.StompName.Length > 0
            ? $"{model.StompName} -> {model.AmpName}"
            : model.AmpName;
    }

    private static void AppendField(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.Append(builder.Length > 0 && builder[^1] is not '\n' ? ", " : " ");
        builder.Append($"{label} « {value} »");
    }
}
