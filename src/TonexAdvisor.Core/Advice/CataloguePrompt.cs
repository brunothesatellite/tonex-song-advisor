using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using TonexAdvisor.Core.Data;
using TonexAdvisor.Core.Data.Records;
using TonexAdvisor.Core.Localization;

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

        var builder = new StringBuilder(46_000);

        builder.AppendLine(CoreTexts.Get("Invite.Catalogue.Role", culture));
        builder.AppendLine(CoreTexts.Get("Invite.Catalogue.Langue", culture));
        builder.AppendLine(CoreTexts.Get("Invite.Catalogue.Style", culture));
        builder.AppendLine(CoreTexts.Get("Invite.Catalogue.Format.Contrainte", culture));
        builder.AppendLine();

        builder.Append(CoreTexts.Get("Invite.Catalogue.Requete", culture));
        AppendField(builder, CoreTexts.Get("Invite.Catalogue.Champ.Artiste", culture), query.Artist, culture);
        AppendField(builder, CoreTexts.Get("Invite.Catalogue.Champ.Chanson", culture), query.Song, culture);
        AppendField(builder, CoreTexts.Get("Invite.Catalogue.Champ.Style", culture), query.Style, culture);
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

        var builder = new StringBuilder(18_000);

        builder.AppendLine(CoreTexts.Get("Invite.Catalogue.Blocs.Titre", culture));
        builder.AppendLine(CoreTexts.Get("Invite.Catalogue.Blocs.Regle", culture));

        foreach (var line in RankAmps(index, culture).Take(MaxAmps))
            builder.AppendLine(line);

        builder.AppendLine();
        builder.AppendLine(CoreTexts.Get("Invite.Catalogue.Baffles.Titre", culture));

        foreach (var cabinet in RankCabs(index).Take(MaxCabs))
            builder.AppendLine(cabinet);

        builder.AppendLine();
        return builder.ToString();
    }

    /// <summary>Amplifiers with their captured stomps, most used first.</summary>
    private static IEnumerable<string> RankAmps(LibraryIndex index, CultureInfo? culture)
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
                    : $"{name} : {CoreTexts.Get("Invite.Catalogue.SansStomp", culture)}";

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

        var format = AnswerFormat.ForCulture(culture);

        builder.AppendLine(CoreTexts.Get("Invite.Catalogue.Regles", culture));
        builder.AppendLine(CoreTexts.Get("Invite.Catalogue.Regles.Bloc", culture));
        builder.AppendLine(CoreTexts.Get("Invite.Catalogue.Regles.Baffle", culture));
        builder.AppendLine(CoreTexts.Get("Invite.Catalogue.Format.Titre", culture));
        builder.AppendLine(CoreTexts.Format("Invite.Catalogue.Format.Bloc", culture, format.Bloc));
        builder.AppendLine(CoreTexts.Format("Invite.Catalogue.Format.Baffle", culture, format.Baffle));
        builder.AppendLine(CoreTexts.Format("Invite.Catalogue.Format.Reglages", culture, format.Reglages));
        builder.AppendLine(CoreTexts.Format("Invite.Catalogue.Format.Alternative", culture, format.Alternative));
        builder.AppendLine(CoreTexts.Format("Invite.Catalogue.Format.Libre", culture, format.ConseilLibre));
        builder.AppendLine(CoreTexts.Format(
            "Invite.Catalogue.Regles.Contrainte", culture,
            format.Bloc, format.Baffle, format.Alternative, format.ConseilLibre));
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

    private static void AppendField(StringBuilder builder, string label, string value, CultureInfo? culture)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.Append(builder.Length > 0 && builder[^1] is not '\n' ? ", " : " ");
        builder.Append(CoreTexts.Format("Invite.Citation", culture, label, value));
    }
}
