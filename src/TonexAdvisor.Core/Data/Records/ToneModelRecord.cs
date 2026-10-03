namespace TonexAdvisor.Core.Data.Records;

/// <summary>
/// A tone model, normalised across library generations.
/// </summary>
/// <remarks>
/// Only metadata is modelled: the encrypted <c>Model</c>/<c>CabModel</c> payloads of V1 are
/// deliberately never read, they are meaningless to the advisor and large.
/// </remarks>
public sealed record ToneModelRecord
{
    /// <summary>GUID string in V1, integer ID string in V2. Matches <see cref="PresetRecord.ToneModelKeys"/>.</summary>
    public required string Key { get; init; }

    /// <summary>The TONEX GUID. Same value in both formats.</summary>
    public string Guid { get; init; } = "";

    public required string Name { get; init; }

    public string AmpName { get; init; } = "";

    public string StompName { get; init; } = "";

    public string AmpChannel { get; init; } = "";

    public string Category { get; init; } = "";

    public string CabCategory { get; init; } = "";

    public string CabName { get; init; } = "";

    public string Mic1 { get; init; } = "";

    public string Mic2 { get; init; } = "";

    public string Outboard { get; init; } = "";

    public string Keywords { get; init; } = "";

    public string Description { get; init; } = "";

    public string ModelComment { get; init; } = "";

    public string CabComment { get; init; } = "";

    /// <summary>Visual skin TONEX applies to the amp block.</summary>
    public string Skin { get; init; } = "";

    public string DateAdded { get; init; } = "";

    public bool Favorite { get; init; }

    /// <summary>Original author's account identifier; kept for display only.</summary>
    public string Copyright { get; init; } = "";

    /// <summary>Resolved nickname of <see cref="Copyright"/> when the library knows it.</summary>
    public string Author { get; init; } = "";

    public ToneModelKind Kind { get; init; }

    /// <summary>Display order string from the library, e.g. <c>0 - AmpAndCab</c>.</summary>
    public string KindOrder { get; init; } = "";

    /// <summary>Folders this tone model is filed under; may be empty.</summary>
    public IReadOnlyList<string> Folders { get; init; } = Array.Empty<string>();

    /// <summary>Premium collection the model belongs to, when any.</summary>
    public string? Collection { get; init; }

    /// <summary>True when the capture includes a cabinet.</summary>
    public bool IncludesCabinet => Kind is ToneModelKind.AmpAndCab or ToneModelKind.ComplexRig or ToneModelKind.CustomIR
                                   || CabName.Length > 0;
}
