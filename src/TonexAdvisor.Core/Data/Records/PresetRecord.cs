namespace TonexAdvisor.Core.Data.Records;

/// <summary>
/// A preset, normalised across library generations.
/// </summary>
/// <remarks>
/// Everything the presentation and recommendation layers need is reachable without knowing
/// whether the source was V1 or V2; <see cref="HasKnobSettings"/> and <see cref="Chain"/> simply
/// stay empty for the format that does not carry them.
/// </remarks>
public sealed record PresetRecord
{
    /// <summary>
    /// Stable identifier within its own database: the GUID in V1, the integer ID in V2.
    /// </summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>Author as signed in TONEX, when the preset declares one.</summary>
    public string UserName { get; init; } = "";

    public string Category { get; init; } = "";

    public string Genre { get; init; } = "";

    public string Artist { get; init; } = "";

    public string Album { get; init; } = "";

    public string Song { get; init; } = "";

    public string Description { get; init; } = "";

    public string Instrument { get; init; } = "";

    public string DateAdded { get; init; } = "";

    public bool Favorite { get; init; }

    /// <summary>Folders this preset is filed under; may be empty.</summary>
    public IReadOnlyList<string> Folders { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Keys of the tone models this preset uses - one entry in V1, one or two in V2
    /// (amplifier and cabinet captured separately).
    /// </summary>
    public IReadOnlyList<string> ToneModelKeys { get; init; } = Array.Empty<string>();

    /// <summary>V2 signal chain. Always empty in V1.</summary>
    public IReadOnlyList<ChainBlock> Chain { get; init; } = Array.Empty<ChainBlock>();

    /// <summary>Knob values. Always null in V2, which stores none.</summary>
    public PresetSettings? Settings { get; init; }

    /// <summary>True when numeric knob values are available for this preset.</summary>
    public bool HasKnobSettings => Settings is not null;

    /// <summary>Chain blocks that are switched on, in order.</summary>
    public IEnumerable<ChainBlock> ActiveChain => Chain.Where(block => !block.Bypass && !block.IsEmpty);
}
