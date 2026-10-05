using System.Globalization;

namespace TonexAdvisor.Core.Advice;

/// <summary>
/// The single source of the answer format: its three layers live here — the marker names the
/// invite asks the model to write, the candidates extraction looks for, and the labels colouring
/// paints. Never a scattered <c>"BLOC :"</c> anywhere else.
/// </summary>
/// <remarks>
/// Core knows no UI: the session's culture comes by parameter (§8.1) and follows the app's rule —
/// a culture starting with « fr » speaks French, anything else speaks English (§11). The invite
/// alone follows the session's language; extraction and colouring always tolerate <b>both</b>
/// marker languages, so a model answering in English inside a French session (or the reverse)
/// still parses, colours and survives.
/// </remarks>
public sealed class AnswerFormat
{
    /// <summary>The six markers: French name first, English name second.</summary>
    private static readonly (string Fr, string En)[] Pairs =
    [
        ("BLOC", "BLOCK"),
        ("BAFFLE", "CAB"),
        ("RÉGLAGES", "SETTINGS"),
        ("ALTERNATIVE", "ALTERNATIVE"),
        ("CONSEIL LIBRE", "FREE ADVICE"),
        ("VERDICT", "VERDICT"),
    ];

    /// <summary>
    /// Layer 3 — every label colouring paints, both languages, stable order.
    /// </summary>
    public static IReadOnlyList<string> AllMarkers { get; } =
    [
        .. Pairs
            .SelectMany(pair => new[] { Label(pair.Fr), Label(pair.En) })
            .Distinct(StringComparer.Ordinal),
    ];

    /// <summary>The protocol as a French session sees it.</summary>
    public static AnswerFormat French { get; } = new(english: false);

    /// <summary>The protocol as any non-French session sees it.</summary>
    public static AnswerFormat English { get; } = new(english: true);

    /// <summary>The session's format: French when the culture starts with « fr », English otherwise.</summary>
    public static AnswerFormat ForCulture(CultureInfo? culture)
        => IsEnglish(culture) ? English : French;

    /// <summary>True when the culture asks for English — no culture still means French.</summary>
    public static bool IsEnglish(CultureInfo? culture)
        => culture is not null
           && !culture.Name.StartsWith("fr", StringComparison.OrdinalIgnoreCase);

    private AnswerFormat(bool english)
    {
        var names = Pairs.Select(pair => english ? pair.En : pair.Fr).ToArray();
        var others = Pairs.Select(pair => english ? pair.Fr : pair.En).ToArray();

        Bloc = names[0];
        Baffle = names[1];
        Reglages = names[2];
        Alternative = names[3];
        ConseilLibre = names[4];
        Verdict = names[5];

        // Layer 2 — extraction: the session's own markers first, the other language as the
        // fallback, so both spellings of the answer are searched in one pass.
        VoiceStarts = [.. new[] { Label(names[0]), Label(others[0]) }.Distinct(StringComparer.Ordinal)];
        VerdictStarts = [Label(names[5])];
        EndMarkers = [.. new[] { names[4], others[4] }.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Layer 1 — the marker the invite asks for, in the session's language.</summary>
    public string Bloc { get; }

    /// <inheritdoc cref="Bloc"/>
    public string Baffle { get; }

    /// <inheritdoc cref="Bloc"/>
    public string Reglages { get; }

    /// <inheritdoc cref="Bloc"/>
    public string Alternative { get; }

    /// <inheritdoc cref="Bloc"/>
    public string ConseilLibre { get; }

    /// <inheritdoc cref="Bloc"/>
    public string Verdict { get; }

    /// <summary>Layer 2 — where a voice's answer block starts, session marker first.</summary>
    public IReadOnlyList<string> VoiceStarts { get; }

    /// <summary>Layer 2 — where the arbiter's block starts.</summary>
    public IReadOnlyList<string> VerdictStarts { get; }

    /// <summary>
    /// Layer 2 — where the answer ends: the free advice paragraph, by its name, colon optional.
    /// </summary>
    public IReadOnlyList<string> EndMarkers { get; }

    /// <summary>Keeps a voice's answer block, tolerating both marker languages.</summary>
    public string ExtractVoice(string? text)
        => AnswerCleaner.Extract(text, VoiceStarts, EndMarkers);

    /// <summary>Keeps the arbiter's verdict block, tolerating both marker languages.</summary>
    public string ExtractVerdict(string? text)
        => AnswerCleaner.Extract(text, VerdictStarts, EndMarkers);

    private static string Label(string name) => $"{name} :";
}
