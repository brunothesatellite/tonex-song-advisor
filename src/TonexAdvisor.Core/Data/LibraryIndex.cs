using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Data;

/// <summary>
/// An in-memory index over the loaded library: fast text search, facets and preset to tone
/// model joins.
/// </summary>
/// <remarks>
/// Built once when the database is opened. It is what lets the advisor shortlist a handful of
/// plausible presets before any model is called, so a 2 500 row library never has to fit in a
/// prompt.
/// </remarks>
public sealed class LibraryIndex
{
    private readonly Dictionary<string, ToneModelRecord> _modelsByKey;
    private readonly Dictionary<string, List<PresetRecord>> _presetsByModelKey;
    private readonly List<IndexEntry> _entries;

    public LibraryIndex(IReadOnlyList<PresetRecord> presets, IReadOnlyList<ToneModelRecord> toneModels)
    {
        ArgumentNullException.ThrowIfNull(presets);
        ArgumentNullException.ThrowIfNull(toneModels);

        Presets = presets;
        ToneModels = toneModels;
        Ranges = ParameterRangeCalculator.Calculate(presets);

        _modelsByKey = new Dictionary<string, ToneModelRecord>(toneModels.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var model in toneModels)
        {
            if (model.Key.Length > 0)
                _modelsByKey.TryAdd(model.Key, model);
        }

        _presetsByModelKey = new Dictionary<string, List<PresetRecord>>(StringComparer.OrdinalIgnoreCase);
        _entries = new List<IndexEntry>(presets.Count);

        foreach (var preset in presets)
        {
            foreach (var key in preset.ToneModelKeys)
            {
                if (!_presetsByModelKey.TryGetValue(key, out var linked))
                    _presetsByModelKey[key] = linked = new List<PresetRecord>();
                linked.Add(preset);
            }

            _entries.Add(new IndexEntry(preset, BuildHaystack(preset)));
        }
    }

    public IReadOnlyList<PresetRecord> Presets { get; }

    public IReadOnlyList<ToneModelRecord> ToneModels { get; }

    /// <summary>Observed span of every numeric parameter, for drawing potentiometers.</summary>
    public IReadOnlyDictionary<string, ParameterRange> Ranges { get; }

    public int PresetCount => Presets.Count;

    public int ToneModelCount => ToneModels.Count;

    /// <summary>Resolves a preset's linked tone models, skipping keys that no longer exist.</summary>
    public IReadOnlyList<ToneModelRecord> ModelsFor(PresetRecord preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var models = new List<ToneModelRecord>(preset.ToneModelKeys.Count);
        foreach (var key in preset.ToneModelKeys)
        {
            if (_modelsByKey.TryGetValue(key, out var model))
                models.Add(model);
        }

        return models;
    }

    /// <summary>Resolves a single tone model by key.</summary>
    public ToneModelRecord? Model(string key)
        => key.Length > 0 && _modelsByKey.TryGetValue(key, out var model) ? model : null;

    /// <summary>Presets that reference a given tone model.</summary>
    public IReadOnlyList<PresetRecord> PresetsFor(string toneModelKey)
        => toneModelKey.Length > 0 && _presetsByModelKey.TryGetValue(toneModelKey, out var presets)
            ? presets
            : Array.Empty<PresetRecord>();

    /// <summary>
    /// Case- and accent-insensitive search. Returns every preset when the query is blank,
    /// otherwise only those whose haystack contains every query token.
    /// </summary>
    public IReadOnlyList<PresetRecord> Search(string? query, int limit = int.MaxValue)
    {
        var tokens = Tokenizer.Tokenize(query);
        if (tokens.Count == 0)
            return limit >= Presets.Count ? Presets : Presets.Take(limit).ToList();

        var results = new List<PresetRecord>();
        foreach (var entry in _entries)
        {
            if (tokens.All(token => entry.Haystack.Contains(token, StringComparison.Ordinal)))
            {
                results.Add(entry.Preset);
                if (results.Count >= limit)
                    break;
            }
        }

        return results;
    }

    private static string BuildHaystack(PresetRecord preset)
    {
        var parts = new List<string>(12)
        {
            preset.Name, preset.UserName, preset.Category, preset.Genre,
            preset.Artist, preset.Album, preset.Song, preset.Description,
            preset.Instrument, string.Join(' ', preset.Folders),
        };

        return Tokenizer.Normalize(string.Join(' ', parts));
    }

    private readonly record struct IndexEntry(PresetRecord Preset, string Haystack);
}
