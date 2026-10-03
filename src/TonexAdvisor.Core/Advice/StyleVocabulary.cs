using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Advice;

/// <param name="Label">French name of the style, reused in the explanations.</param>
/// <param name="TargetSaturation">
/// Where on the saturation ladder this style sits, 0 (clean) to 1 (high gain).
/// </param>
/// <param name="Keywords">Already normalised keywords that recognise the style.</param>
public sealed record StyleProfile(string Label, double TargetSaturation, IReadOnlyList<string> Keywords);

/// <summary>
/// Maps free-form style words onto the saturation ladder TONEX uses for its categories.
/// </summary>
/// <remarks>
/// This is the pivot of the local advice: <c>Genre</c> is <c>None</c> for 1 502 of 2 310 presets,
/// so the category (<c>HI-GAIN</c>, <c>DRIVE</c>, <c>CLEAN</c>, <c>STOMP - …</c>) is the only
/// signal that is filled in for practically every preset. The numeric <c>ModelGain</c> knob was
/// tested and discarded: its median is 5 for every category, it carries almost no information.
/// </remarks>
public static class StyleVocabulary
{
    /// <summary>Normalised category name to its position on the saturation ladder.</summary>
    private static readonly Dictionary<string, double> SaturationByCategory = new(StringComparer.Ordinal)
    {
        ["clean"] = 0.15,
        ["stomp eq"] = 0.30,
        ["stomp overdrive"] = 0.50,
        ["drive"] = 0.60,
        ["stomp distortion"] = 0.80,
        ["fuzzy"] = 0.90,
        ["stomp fuzz"] = 0.92,
        ["hi gain"] = 1.00,
    };

    /// <summary>
    /// Generic styles first, specific ones last: a tie is resolved towards the end of the list,
    /// so « rock metal » reads as metal and « clean jazz » as jazz.
    /// </summary>
    private static readonly StyleProfile[] Profiles =
    [
        new("propre", 0.15,
            ["clean", "jazz", "funk", "pop", "acoustic", "country", "ambient", "ballad", "soul", "gospel"]),
        new("blues", 0.45, ["blues", "bluesy"]),
        new("rock", 0.60,
            ["rock", "grunge", "indie", "alternative", "garage", "classic rock", "hard rock"]),
        new("crunch", 0.65, ["drive", "crunch", "overdrive", "retro", "80s", "pushed"]),
        new("distorsion", 0.85, ["distortion", "punk", "industrial", "heavy"]),
        new("metal", 1.00,
            ["metal", "djent", "thrash", "death", "black", "hardcore", "doom", "shred",
             "solo", "lead", "high gain", "hi gain", "progressive"]),
        new("fuzz", 0.92, ["fuzz", "fuzzy", "velvet"]),
    ];

    /// <summary>
    /// Recognises the style, or returns null when the wording carries no known signal — the
    /// caller then simply drops the saturation signal instead of guessing.
    /// </summary>
    public static StyleProfile? Detect(string? style)
    {
        var normalized = Tokenizer.Normalize(style);
        if (normalized.Length == 0)
            return null;

        StyleProfile? best = null;
        var bestHits = 0;

        foreach (var profile in Profiles)
        {
            var hits = profile.Keywords.Count(keyword => normalized.Contains(keyword, StringComparison.Ordinal));
            if (hits >= bestHits && hits > 0)
            {
                best = profile;
                bestHits = hits;
            }
        }

        return best;
    }

    /// <summary>Position of a TONEX category on the 0..1 saturation ladder.</summary>
    public static double CategorySaturation(string? category)
    {
        var normalized = Tokenizer.Normalize(category);
        if (normalized.Length == 0)
            return NeutralSaturation;

        if (SaturationByCategory.TryGetValue(normalized, out var known))
            return known;

        // Unknown wording: fall back on the ladder's order rather than on a guess.
        if (normalized.Contains("hi gain", StringComparison.Ordinal)) return 1.00;
        if (normalized.Contains("fuzz", StringComparison.Ordinal)) return 0.90;
        if (normalized.Contains("distortion", StringComparison.Ordinal)) return 0.80;
        if (normalized.Contains("overdrive", StringComparison.Ordinal)) return 0.50;
        if (normalized.Contains("clean", StringComparison.Ordinal)) return 0.15;
        if (normalized.Contains("drive", StringComparison.Ordinal)) return 0.60;

        return NeutralSaturation;
    }

    /// <summary>Used for categories the vocabulary does not know, and for missing categories.</summary>
    public static double NeutralSaturation => 0.60;

    /// <summary>
    /// True for the pedals a metal player puts in front of a high-gain amp to tighten the bass —
    /// the one combination this advisor will recommend beyond simple matching.
    /// </summary>
    public static bool IsFrontBoost(string? stomp)
    {
        var normalized = Tokenizer.Normalize(stomp);
        if (normalized.Length == 0)
            return false;

        return normalized.Contains("boost", StringComparison.Ordinal)
               || normalized.Contains("tube screamer", StringComparison.Ordinal)
               || normalized.Contains("808", StringComparison.Ordinal)
               || normalized.Contains("ts9", StringComparison.Ordinal)
               || normalized.Contains("sd 1", StringComparison.Ordinal)
               || normalized.Contains("klon", StringComparison.Ordinal)
               || normalized.Contains("centaur", StringComparison.Ordinal);
    }
}
