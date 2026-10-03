using System.Text;
using TonexAdvisor.Core.Data;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Advice;

/// <summary>
/// Builds the prompt sent to the AIs: the request plus the <b>whole</b> catalogue of what the
/// library holds, names only.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately richer than the local shortlist: the local ranking matches words, the AIs match
/// meaning — « un son Slash » finds « AFD100 » in this list and never would in a token match.
/// </para>
/// <para>
/// Names only, no metadata: measured on a 3 097 models library that is 894 blocks and 557
/// cabinets, about 9 600 tokens. Blocks are listed as <c>stomp -&gt; ampli</c> pairs and never as
/// two separate lists, because the two are captured together and cannot be mixed — sending three
/// flat lists would invite exactly the combination TONEX refuses.
/// </para>
/// </remarks>
public static class CataloguePrompt
{
    /// <summary>
    /// Answer budget. The answer is one block, one cabinet and a few settings: it stays short,
    /// but a reasoning model spends its budget before writing.
    /// </summary>
    public const int MaxTokens = 4000;

    /// <summary>French prompt for one voice.</summary>
    public static string Build(AdviceQuery query, LibraryIndex index)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(index);

        var builder = new StringBuilder(46_000);

        builder.AppendLine("Tu es un conseiller guitare (rock/metal) qui connaît les bibliothèques TONEX.");
        builder.AppendLine("Réponds en français, en 8 à 12 phrases, ton direct et concret.");
        builder.AppendLine("Va droit au but : ni préambule, ni analyse de la demande.");
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
    /// The catalogue itself — the captured blocks and the cabinets, names only. Shared with the
    /// arbitration so every voice works on the same material.
    /// </summary>
    public static string DescribeCatalogue(LibraryIndex index)
    {
        ArgumentNullException.ThrowIfNull(index);

        var builder = new StringBuilder(40_000);

        var blocks = index.ToneModels
            .Select(BlockName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var cabinets = index.ToneModels
            .Select(model => model.CabName)
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        builder.AppendLine("Blocs capturés disponibles — stomp + ampli indissociables :");
        foreach (var block in blocks)
            builder.AppendLine(block);

        builder.AppendLine();
        builder.AppendLine("Baffles disponibles — interchangeables :");
        foreach (var cabinet in cabinets)
            builder.AppendLine(cabinet);

        builder.AppendLine();
        return builder.ToString();
    }

    /// <summary>The constraint every answer has to respect, and the format it must answer in.</summary>
    public static void AppendRules(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AppendLine("Règles :");
        builder.AppendLine(
            "- Un bloc est un couple stomp + ampli capturé ensemble : il est inséparable. Ne mélange " +
            "jamais un stomp d'un bloc avec l'ampli d'un autre.");
        builder.AppendLine("- Le baffle est libre : choisis celui qui sert le mieux le style, dans la liste.");
        builder.AppendLine("- Réponds exactement dans ce format, en reprenant les noms tels quels :");
        builder.AppendLine("  BLOC : <nom du bloc choisi>");
        builder.AppendLine("  BAFFLE : <nom du baffle choisi>");
        builder.AppendLine("  RÉGLAGES : 2 à 3 réglages concrets (gain, EQ, réverb/delay)");
        builder.AppendLine("  ALTERNATIVE : <un autre bloc de la liste>");
        builder.AppendLine("- N'invente aucun nom absent des listes ci-dessus.");
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
