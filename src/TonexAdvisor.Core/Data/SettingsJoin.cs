using TonexAdvisor.Core.Data.Records;
using TonexAdvisor.Core.Localization;

namespace TonexAdvisor.Core.Data;

/// <summary>
/// Copies the knob values of a generation 1 library onto the presets of another library.
/// </summary>
/// <remarks>
/// <para>
/// Generation 2 does not carry them: its <c>Chain</c> field only lists the active blocks, and the
/// values live in IK's encrypted <c>.txp</c> files (2 513 of them in
/// <c>TONEX\Library\Presets</c>, one per preset). But a V2 library is usually an upgrade of a V1
/// one — the same presets, values in the clear — so the two can be joined.
/// </para>
/// <para>
/// The join happens in memory, on records: neither library is opened for writing nor modified.
/// Presets are matched by name first, then by the GUID of their tone model, which is the one
/// identifier IK keeps identical across both formats.
/// </para>
/// </remarks>
public static class SettingsJoin
{
    /// <summary>
    /// The mention shown when the values came from the joined library — composed in the current
    /// language when the join runs (in memory only: the libraries are never written to).
    /// </summary>
    public static string Origin => CoreTexts.Get("Origine.BiblioV1");

    /// <summary>
    /// The generation 1 library to take the values from, when it sits next to the generation 2
    /// one (<c>Library.db</c> beside <c>Library2.db</c> — the case both in the workspace and in
    /// the TONEX folder).
    /// </summary>
    public static string? FindCompanionPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (string.IsNullOrEmpty(directory))
            return null;

        var candidate = Path.Combine(directory, "Library.db");
        if (!File.Exists(candidate))
            return null;

        return string.Equals(Path.GetFullPath(candidate), Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase)
            ? null
            : candidate;
    }

    /// <summary>
    /// The presets of <paramref name="presets"/> with the settings of the matching presets of
    /// <paramref name="sourcePresets"/>, in the original order. Enriched presets say where their
    /// values come from (<see cref="Origin"/>).
    /// </summary>
    public static IReadOnlyList<PresetRecord> WithSettingsFrom(
        IReadOnlyList<PresetRecord> presets,
        IReadOnlyList<ToneModelRecord> models,
        IReadOnlyList<PresetRecord> sourcePresets,
        IReadOnlyList<ToneModelRecord> sourceModels)
    {
        ArgumentNullException.ThrowIfNull(presets);
        ArgumentNullException.ThrowIfNull(models);
        ArgumentNullException.ThrowIfNull(sourcePresets);
        ArgumentNullException.ThrowIfNull(sourceModels);

        var guidOf = MapGuids(models);
        var sourceGuidOf = MapGuids(sourceModels);

        var byName = new Dictionary<string, PresetRecord>(StringComparer.OrdinalIgnoreCase);
        var byGuid = new Dictionary<string, PresetRecord>(StringComparer.OrdinalIgnoreCase);

        foreach (var preset in sourcePresets)
        {
            if (preset.Settings is null || preset.Name.Length == 0)
                continue;

            if (!byName.ContainsKey(preset.Name))
                byName[preset.Name] = preset;

            foreach (var key in preset.ToneModelKeys)
            {
                if (sourceGuidOf.TryGetValue(key, out var guid) && guid.Length > 0 && !byGuid.ContainsKey(guid))
                    byGuid[guid] = preset;
            }
        }

        var result = new List<PresetRecord>(presets.Count);

        foreach (var preset in presets)
        {
            // Une bibliothèque V1 enrichie de ses propres valeurs ne se voit pas voler celles
            // d'une autre : on ne comble que ce qui manque.
            if (preset.Settings is not null)
            {
                result.Add(preset);
                continue;
            }

            var source = Match(preset, byName, byGuid, guidOf);
            result.Add(source?.Settings is { } settings
                ? preset with { Settings = new PresetSettings(settings.Numbers, settings.Texts, Origin) }
                : preset);
        }

        return result;
    }

    private static PresetRecord? Match(
        PresetRecord preset,
        IReadOnlyDictionary<string, PresetRecord> byName,
        IReadOnlyDictionary<string, PresetRecord> byGuid,
        IReadOnlyDictionary<string, string> guidOf)
    {
        if (preset.Name.Length > 0 && byName.TryGetValue(preset.Name, out var byNameMatch))
            return byNameMatch;

        foreach (var key in preset.ToneModelKeys)
        {
            if (guidOf.TryGetValue(key, out var guid) && byGuid.TryGetValue(guid, out var byGuidMatch))
                return byGuidMatch;
        }

        return null;
    }

    /// <summary>Model key → TONEX GUID, the identifier shared by both formats.</summary>
    private static Dictionary<string, string> MapGuids(IReadOnlyList<ToneModelRecord> models)
    {
        var guids = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var model in models)
        {
            if (model.Key.Length > 0 && model.Guid.Length > 0 && !guids.ContainsKey(model.Key))
                guids[model.Key] = model.Guid;
        }

        return guids;
    }
}
