using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Advice;

/// <param name="Text">French sentence shown to the user.</param>
/// <param name="Points">Points this signal contributed to the raw score.</param>
public sealed record ScoreReason(string Text, double Points);

/// <param name="Preset">The preset that was scored.</param>
/// <param name="Score">0..100, normalised against what this particular query could reach.</param>
/// <param name="Reasons">Every signal that fired, best first.</param>
public sealed record ScoredPreset(
    PresetRecord Preset,
    double Score,
    IReadOnlyList<ScoreReason> Reasons);

/// <param name="Amp">Amplifier of the winning capture, empty when the model is a stomp only.</param>
/// <param name="Stomp">Stomp captured in front of that very amplifier: the two come from a single
/// capture and are never borrowed from two different ones.</param>
/// <param name="Cab">Recommended cabinet — free choice, it does not have to come from the capture.
/// Empty only when the library holds no cabinet at all.</param>
/// <param name="CabNote">French line saying why this cabinet, and that it may be swapped.</param>
/// <param name="Score">0..100, normalised against what this query could reach.</param>
/// <param name="Reasons">Every signal that fired, best first.</param>
/// <param name="Example">Best tone model of the group, used to open the detail panel.</param>
/// <param name="PresetCount">How many presets in the library use this combination.</param>
public sealed record ScoredCombination(
    string Amp,
    string Stomp,
    string Cab,
    string CabNote,
    double Score,
    IReadOnlyList<ScoreReason> Reasons,
    ToneModelRecord Example,
    int PresetCount);
