using CommunityToolkit.Mvvm.ComponentModel;
using TonexAdvisor.Core.Data;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.App.ViewModels;

/// <summary>Everything the detail panel shows for one preset.</summary>
public sealed partial class PresetDetailViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HardwareSummary))]
    private bool _showHardware;

    private PresetDetailViewModel(
        PresetRecord preset,
        IReadOnlyList<ToneModelCardViewModel> toneModels,
        IReadOnlyList<SettingSectionViewModel> sections,
        IReadOnlyList<SettingSectionViewModel> hardwareA,
        IReadOnlyList<SettingSectionViewModel> hardwareB)
    {
        Preset = preset;
        ToneModels = toneModels;
        Sections = sections;
        HardwareASections = hardwareA;
        HardwareBSections = hardwareB;
    }

    public PresetRecord Preset { get; }

    public string Name => Preset.Name;

    public string Key => Preset.Key;

    public string Category => Preset.Category;

    public string Genre => Preset.Genre;

    public string Artist => Preset.Artist;

    public string Album => Preset.Album;

    public string Song => Preset.Song;

    public string Description => Preset.Description;

    public string UserName => Preset.UserName;

    public string DateAdded => Preset.DateAdded;

    public bool Favorite => Preset.Favorite;

    public string Folders => Preset.Folders.Count > 0 ? string.Join(" · ", Preset.Folders) : "—";

    public string Instrument => Preset.Instrument;

    public IReadOnlyList<ToneModelCardViewModel> ToneModels { get; }

    public IReadOnlyList<SettingSectionViewModel> Sections { get; }

    public IReadOnlyList<SettingSectionViewModel> HardwareASections { get; }

    public IReadOnlyList<SettingSectionViewModel> HardwareBSections { get; }

    public bool HasSettings => Preset.HasKnobSettings;

    public bool HasHardware => HardwareASections.Count > 0 || HardwareBSections.Count > 0;

    /// <summary>Toggle label for the hardware slots.</summary>
    public string HardwareSummary => ShowHardware
        ? "Masquer les réglages matériels"
        : $"Afficher les réglages matériels ({HardwareASections.Count + HardwareBSections.Count} blocs)";

    public bool HasChain => Preset.Chain.Count > 0;

    public string ChainSummary => HasChain
        ? string.Join("  ·  ", Preset.ActiveChain.Select(block => block.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        : "—";

    public string ActiveBlockCount => HasChain
        ? Preset.ActiveChain.Count().ToString(System.Globalization.CultureInfo.InvariantCulture)
        : "—";

    public string ActiveBlockLine => HasChain
        ? ActiveBlockCount + " bloc(s) actif(s) sur la chaîne"
        : "";

    /// <summary>Explains a missing detail panel rather than showing an empty one.</summary>
    public string SettingsNote => HasSettings
        ? ""
        : "Bibliothèque de génération 2 : TONEX n'y stocke aucun réglage numérique. " +
          "Seuls le preset, le tone model, le baffle et l'ordre des blocs sont connus.";

    public string TagsLine
    {
        get
        {
            var parts = new List<string>(5);
            if (Category.Length > 0) parts.Add(Category);
            if (Genre.Length > 0 && Genre != "None") parts.Add(Genre);
            if (Artist.Length > 0) parts.Add(Artist);
            if (Song.Length > 0) parts.Add("« " + Song + " »");
            if (Album.Length > 0) parts.Add(Album);
            return parts.Count > 0 ? string.Join("  ·  ", parts) : "—";
        }
    }

    /// <summary>Builds a detail view from a preset, its tone models and the observed ranges.</summary>
    public static PresetDetailViewModel Create(
        PresetRecord preset,
        IReadOnlyList<ToneModelRecord> toneModels,
        IReadOnlyDictionary<string, ParameterRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(preset);
        ArgumentNullException.ThrowIfNull(toneModels);
        ArgumentNullException.ThrowIfNull(ranges);

        var cards = toneModels.Select(model => new ToneModelCardViewModel(model)).ToList();

        if (!preset.HasKnobSettings)
        {
            return new PresetDetailViewModel(
                preset,
                cards,
                Array.Empty<SettingSectionViewModel>(),
                Array.Empty<SettingSectionViewModel>(),
                Array.Empty<SettingSectionViewModel>());
        }

        return new PresetDetailViewModel(
            preset,
            cards,
            BuildSections(preset, ranges, KnobCatalog.Sections, prefix: ""),
            BuildSections(preset, ranges, KnobCatalog.HardwareASections, prefix: "HWParamA_"),
            BuildSections(preset, ranges, KnobCatalog.HardwareBSections, prefix: "HWParamB_"));
    }

    private static IReadOnlyList<SettingSectionViewModel> BuildSections(
        PresetRecord preset,
        IReadOnlyDictionary<string, ParameterRange> ranges,
        IReadOnlyList<SettingSection> catalog,
        string prefix)
    {
        var settings = preset.Settings!;
        var sections = new List<SettingSectionViewModel>(catalog.Count);

        foreach (var section in catalog)
        {
            // A hardware slot that stores no parameter at all is simply not there.
            if (prefix.Length > 0 && section.Knobs.All(knob => settings.Number(knob.Param) is null))
                continue;

            var knobs = new List<KnobViewModel>(section.Knobs.Count);
            foreach (var definition in section.Knobs)
            {
                var knob = BuildKnob(definition, settings, ranges);
                if (knob is not null)
                    knobs.Add(knob);
            }

            if (knobs.Count == 0)
                continue;

            bool? enabled = null;
            if (section.EnableParam is not null && settings.HasNumber(section.EnableParam))
                enabled = settings.IsEnabled(section.EnableParam);

            string? position = null;
            if (section.PositionParam is not null && settings.HasNumber(section.PositionParam))
                position = settings.Number(section.PositionParam) > 0 ? "POST" : "PRÉ";

            sections.Add(new SettingSectionViewModel(section.Key, section.Title, enabled, position, knobs));
        }

        return sections;
    }

    private static KnobViewModel? BuildKnob(
        KnobDefinition definition,
        PresetSettings settings,
        IReadOnlyDictionary<string, ParameterRange> ranges)
    {
        if (definition.Shape == KnobShape.Switch)
        {
            if (!settings.HasNumber(definition.Param))
                return null;

            var isOn = settings.IsEnabled(definition.Param);
            return new KnobViewModel(
                definition.Param, definition.Label, definition.Shape,
                isOn ? "ON" : "OFF", "", isOn ? 1d : 0d, isOn ? 1d : 0d, 0d, 1d, isOn);
        }

        var value = settings.Number(definition.Param);
        if (value is null)
            return settings.Text(definition.Param) is { } text && text.Length > 0
                ? new KnobViewModel(
                    definition.Param, definition.Label, definition.Shape, text, "",
                    0d, 0.5d, 0d, 1d, false)
                : null;

        ranges.TryGetValue(definition.Param, out var range);
        var minimum = range.Span > 0 ? range.Min : 0d;
        var maximum = range.Span > 0 ? range.Max : Math.Max(1d, value.Value);

        return new KnobViewModel(
            definition.Param,
            definition.Label,
            definition.Shape,
            PresetSettings.Format(value.Value),
            definition.Unit ?? "",
            value.Value,
            range.Span > 0 ? range.Normalize(value.Value) : 0.5d,
            minimum,
            maximum,
            definition.Shape == KnobShape.Knob && value.Value > 0);
    }
}
