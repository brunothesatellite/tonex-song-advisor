namespace TonexAdvisor.Core.Data;

/// <summary>
/// Identifies which generation of the TONEX library database is being read.
/// </summary>
/// <remarks>
/// <para>
/// <b>V1</b> (<c>Library.db</c>) stores every preset parameter as its own column on the
/// <c>Presets</c> table (344 columns) and links a preset to its tone model through
/// <c>Presets.ToneModel_GUID</c>. Tone model payloads live in <c>ToneModels.Model</c> /
/// <c>ToneModels.CabModel</c>.
/// </para>
/// <para>
/// <b>V2</b> (<c>Library2.db</c>) drops all of that: <c>Presets</c> only keeps tags plus a
/// <c>Chain</c> JSON blob describing which blocks are active, and presets are linked to tone
/// models through the <c>PresetsToneModelsMatch</c> join table. No knob values exist in the file.
/// </para>
/// </remarks>
public enum DatabaseFormat
{
    /// <summary>The schema matches neither known generation.</summary>
    Unknown = 0,

    /// <summary>Classic <c>Library.db</c>: flat preset parameters, GUID keys.</summary>
    V1 = 1,

    /// <summary><c>Library2.db</c>: metadata only, chain JSON, integer keys.</summary>
    V2 = 2,
}
