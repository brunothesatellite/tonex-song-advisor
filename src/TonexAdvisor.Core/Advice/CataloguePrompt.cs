using System.Globalization;
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
    /// Answer budget. Deliberately generous: a reasoning model spends most of it on its chain of
    /// thought before writing the first word, and cutting it short truncates the answer mid-word.
    /// Still fits the free tier of Groq (3 400 tokens of catalogue + 4 000 here, under its 8 000
    /// tokens per minute).
    /// </summary>
    public const int MaxTokens = 4000;

    /// <summary>Amplifiers sent to the AIs, most used first.</summary>
    public const int MaxAmps = 200;

    /// <summary>Cabinets sent to the AIs, most used first.</summary>
    public const int MaxCabs = 120;

    /// <summary>Capture names like « 78 Signal Chain » or « 121 » tell the AIs nothing.</summary>
    private static readonly Regex PureNumber = new(@"^\d+$", RegexOptions.Compiled);

    private static readonly Regex SignalChainNoise = new(@"^\d+ Signal Chain$", RegexOptions.Compiled);

    /// <summary>Prompt for one voice, in the language the session speaks (§8.1).</summary>
    public static string Build(AdviceQuery query, LibraryIndex index, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(index);

        var english = AnswerFormat.IsEnglish(culture);
        var builder = new StringBuilder(46_000);

        builder.AppendLine(english
            ? "You are a rock/metal guitar advisor who knows the TONEX libraries."
            : "Tu es un conseiller guitare (rock/metal) qui connaît les bibliothèques TONEX.");
        builder.AppendLine(english
            ? "Answer in English, in 8 to 12 sentences, direct and concrete."
            : "Réponds en français, en 8 à 12 phrases, ton direct et concret.");
        builder.AppendLine(english
            ? "Get straight to the point: no preamble, no analysis of the request."
            : "Va droit au but : ni préambule, ni analyse de la demande.");
        builder.AppendLine(english
            ? "Write only the lines of the format below: no draft, no checking of your own " +
              "format, no sentence counting, no commentary on what you are about to answer."
            : "Écris uniquement les lignes du format ci-dessous : ni brouillon, ni vérification de ton " +
              "propre format, ni décompte de phrases, ni commentaire sur ce que tu t'apprêtes à répondre.");
        builder.AppendLine();

        builder.Append(english ? "The request:" : "La demande :");
        AppendField(builder, english ? "artist" : "artiste", query.Artist, english);
        AppendField(builder, english ? "song" : "chanson", query.Song, english);
        AppendField(builder, "style", query.Style, english);
        builder.AppendLine();

        builder.AppendLine();
        builder.Append(DescribeCatalogue(index, culture));
        AppendRules(builder, culture);

        return builder.ToString();
    }

    /// <summary>
    /// The catalogue itself — the captured blocks and the cabinets, names only, most used first.
    /// Shared with the arbitration so every voice works on the same material.
    /// </summary>
    public static string DescribeCatalogue(LibraryIndex index, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(index);

        var english = AnswerFormat.IsEnglish(culture);
        var builder = new StringBuilder(18_000);

        builder.AppendLine(english
            ? "Captured blocks — stomp + amp, inseparable:"
            : "Blocs capturés disponibles — stomp + ampli indissociables :");
        builder.AppendLine(english
            ? "Each line = an amp, with the stomps captured with it. A block = an amp plus at most one of its stomps."
            : "Chaque ligne = un ampli, avec les stomp capturés avec lui. Un bloc = un ampli + au plus un de ses stomp.");

        foreach (var line in RankAmps(index, english).Take(MaxAmps))
            builder.AppendLine(line);

        builder.AppendLine();
        builder.AppendLine(english
            ? "Cabinets available — interchangeable:"
            : "Baffles disponibles — interchangeables :");

        foreach (var cabinet in RankCabs(index).Take(MaxCabs))
            builder.AppendLine(cabinet);

        builder.AppendLine();
        return builder.ToString();
    }

    /// <summary>Amplifiers with their captured stomps, most used first.</summary>
    private static IEnumerable<string> RankAmps(LibraryIndex index, bool english)
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
                    : $"{name} : {(english ? "(no stomp)" : "(sans stomp)")}";

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
    public static void AppendRules(StringBuilder builder, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var english = AnswerFormat.IsEnglish(culture);
        var format = AnswerFormat.ForCulture(culture);

        builder.AppendLine(english ? "Rules:" : "Règles :");
        builder.AppendLine(english
            ? "- A block = one amp from the list plus at most one of the stomps captured with it. " +
              "Never mix a stomp of another amp: they were captured together."
            : "- Un bloc = un ampli de la liste + au plus un des stomp capturés avec lui. Ne mélange " +
              "jamais un stomp d'un autre ampli : ils ont été capturés ensemble.");
        builder.AppendLine(english
            ? "- The cabinet is free: pick the one that serves the style best, from the list."
            : "- Le baffle est libre : choisis celui qui sert le mieux le style, dans la liste.");
        builder.AppendLine(english
            ? "- Answer exactly in this format, keeping the names as they are:"
            : "- Réponds exactement dans ce format, en reprenant les noms tels quels :");
        builder.AppendLine(english
            ? $"  {format.Bloc} : <stomp> -> <amp>, or <amp> when there is no stomp"
            : $"  {format.Bloc} : <stomp> -> <ampli>, ou <ampli> s'il n'y a pas de stomp");
        builder.AppendLine(english
            ? $"  {format.Baffle} : <name of the chosen cabinet>"
            : $"  {format.Baffle} : <nom du baffle choisi>");
        builder.AppendLine(english
            ? $"  {format.Reglages} : 2 to 3 concrete settings (gain, EQ, reverb/delay)"
            : $"  {format.Reglages} : 2 à 3 réglages concrets (gain, EQ, réverb/delay)");
        builder.AppendLine(english
            ? $"  {format.Alternative} : <another block>"
            : $"  {format.Alternative} : <un autre bloc>");
        builder.AppendLine(english
            ? $"  {format.ConseilLibre} : what you would use yourself, with no library constraint — " +
              "the real gear of the track or the style if you know it (amp, cabinet, pedals, " +
              "tuning), and the typical settings that go with it."
            : $"  {format.ConseilLibre} : ce que tu utiliserais toi, sans aucune contrainte de bibliothèque — " +
              "le matériel réel du morceau ou du style si tu le connais (ampli, baffle, pédales, " +
              "accordage), et les réglages typiques qui vont avec.");
        builder.AppendLine(english
            ? "- Invent no name absent from the lists above: this constraint covers only " +
              $"{format.Bloc}, {format.Baffle} and {format.Alternative}. {format.ConseilLibre}, however, " +
              "is open to everything you know."
            : "- N'invente aucun nom absent des listes ci-dessus : cette contrainte ne vaut que pour " +
              $"{format.Bloc}, {format.Baffle} et {format.Alternative}. {format.ConseilLibre}, lui, est ouvert à tout ton savoir.");
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

    private static void AppendField(StringBuilder builder, string label, string value, bool english)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.Append(builder.Length > 0 && builder[^1] is not '\n' ? ", " : " ");
        builder.Append(english ? $"{label} \"{value}\"" : $"{label} « {value} »");
    }
}
