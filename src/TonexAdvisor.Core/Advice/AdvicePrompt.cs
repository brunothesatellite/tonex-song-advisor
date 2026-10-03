using System.Globalization;
using System.Text;
using TonexAdvisor.Core.Data;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Advice;

/// <summary>
/// Builds the prompt sent to the AI from the local shortlist — never the whole library.
/// </summary>
/// <remarks>
/// The model receives three presets and one captured block, not 2 310 rows: that keeps the
/// request inside a small token budget and stops the answer from inventing presets the user
/// cannot find. The stomp/amp rule is repeated verbatim because it is exactly the kind of
/// constraint a model happily ignores.
/// </remarks>
public static class AdvicePrompt
{
    /// <summary>
    /// Answer budget. Deliberately generous: a reasoning model spends most of it on its chain of
    /// thought before writing the first word of the answer, and cutting it off there returns the
    /// thinking instead of the advice.
    /// </summary>
    public const int MaxTokens = 4000;

    /// <summary>The knobs worth showing: enough to give settings, few enough to stay readable.</summary>
    private static readonly string[] SettingsOfInterest =
    [
        "ModelGain", "ModelVolume", "EqBass", "EqMiddle", "EqTreble",
    ];

    /// <summary>
    /// French prompt for the given query and shortlist. <paramref name="index"/> is optional and
    /// only used to name the amplifier and the cabinet behind each preset.
    /// </summary>
    public static string Build(
        AdviceQuery query,
        IReadOnlyList<ScoredPreset> presets,
        ScoredCombination? combination,
        LibraryIndex? index = null)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(presets);

        var builder = new StringBuilder(1600);

        builder.AppendLine("Tu es un conseiller guitare (rock/metal) pour un lecteur de bibliothèques TONEX.");
        builder.AppendLine("Réponds en français, en 6 à 10 phrases, ton direct et concret.");
        builder.AppendLine("Va droit au but : ni préambule, ni analyse de la demande, ni recapitulatif final.");
        builder.AppendLine();

        builder.Append("La demande :");
        AppendField(builder, "artiste", query.Artist);
        AppendField(builder, "chanson", query.Song);
        AppendField(builder, "style", query.Style);
        builder.AppendLine();

        builder.AppendLine("Classement local (score 0..100) :");

        for (var i = 0; i < presets.Count; i++)
        {
            var scored = presets[i];
            var preset = scored.Preset;

            builder.Append($"{i + 1}. {scored.Score:0} % - preset « {preset.Name} »");
            builder.Append($", catégorie {Label(preset.Category)}");

            if (Label(preset.Genre).Length > 0)
                builder.Append($", genre {preset.Genre}");
            if (preset.Artist.Length > 0)
                builder.Append($", artiste {preset.Artist}");
            if (preset.Song.Length > 0)
                builder.Append($", chanson {preset.Song}");

            builder.AppendLine();

            if (index is not null)
            {
                var models = index.ModelsFor(preset);

                var amp = JoinDistinct(models, model => model.AmpName);
                var cab = JoinDistinct(models, model => model.CabName);

                if (amp.Length > 0)
                    builder.AppendLine($"   ampli {amp}");
                if (cab.Length > 0)
                    builder.AppendLine($"   baffle {cab}");
            }

            var settings = DescribeSettings(preset);
            if (settings.Length > 0)
                builder.AppendLine($"   réglages {settings}");
        }

        if (combination is not null)
        {
            builder.AppendLine();
            builder.Append($"{combination.Score:0} % - bloc capturé : ");
            builder.Append(combination.Stomp.Length > 0
                ? $"{combination.Stomp} -> {combination.Amp}"
                : combination.Amp);
            builder.AppendLine();
            builder.AppendLine($"   baffle suggéré : {Label(combination.Cab)} ({combination.CabNote})");
        }

        builder.AppendLine();
        builder.AppendLine(
            "Règles : le stomp et l'ampli d'une capture sont inséparables, ne propose jamais de " +
            "mélanger un stomp d'un modèle avec un ampli d'un autre ; seul le baffle se change.");
        builder.AppendLine(
            "Ta réponse : choisis un preset de la liste et justifie-le, confirme ou ajuste le bloc " +
            "ampli + baffle, donne 2 à 3 réglages concrets en t'appuyant sur les valeurs ci-dessus, " +
            "puis propose une alternative parmi la liste.");
        builder.AppendLine("N'invente aucun preset, ampli ou baffle qui ne figurent pas ci-dessus.");

        return builder.ToString();
    }

    private static void AppendField(StringBuilder builder, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        builder.Append(builder.Length > 0 && builder[^1] is not '\n' ? ", " : " ");
        builder.Append($"{label} « {value} »");
    }

    /// <summary>Empty metadata reads better as an empty field than as « None ».</summary>
    private static string Label(string value)
        => value is "None" or "" ? "" : value;

    private static string JoinDistinct(
        IReadOnlyList<ToneModelRecord> models,
        Func<ToneModelRecord, string> selector)
        => string.Join(" + ", models
            .Select(selector)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase));

    /// <summary>Turns the knob values into one compact line, when the format carries any.</summary>
    private static string DescribeSettings(PresetRecord preset)
    {
        var settings = preset.Settings;
        if (settings is null)
            return "";

        var parts = new List<string>(SettingsOfInterest.Length);

        foreach (var name in SettingsOfInterest)
        {
            var value = settings.Number(name);
            if (value is null)
                continue;

            parts.Add($"{name}={value.Value.ToString("0.0", CultureInfo.InvariantCulture)}");
        }

        return string.Join(", ", parts);
    }
}
