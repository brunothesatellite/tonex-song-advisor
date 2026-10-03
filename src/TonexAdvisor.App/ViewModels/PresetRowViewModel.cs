using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.App.ViewModels;

/// <summary>One row of the presets grid.</summary>
public sealed class PresetRowViewModel
{
    public PresetRowViewModel(PresetRecord preset, IReadOnlyList<ToneModelRecord> toneModels)
    {
        Record = preset;
        ToneModels = toneModels;

        Name = preset.Name;
        Category = preset.Category;
        Genre = preset.Genre == "None" ? "" : preset.Genre;
        Artist = preset.Artist;
        Song = preset.Song;
        Folders = preset.Folders.Count > 0 ? preset.Folders[0] : "";
        Author = preset.UserName;
        Amp = JoinDistinct(toneModels, model => model.AmpName);
        Cab = JoinDistinct(toneModels, model => model.CabName);
        Mics = JoinDistinct(toneModels, model => model.Mic1);
        Kind = string.Join(" + ", toneModels
            .Select(model => model.Kind)
            .Distinct()
            .Select(KindLabel));

        HasSettings = preset.HasKnobSettings;
        ModelCount = toneModels.Count;
        Favorite = preset.Favorite;

        if (Author.Length == 0)
            Author = JoinDistinct(toneModels, model => model.Author);
    }

    public PresetRecord Record { get; }

    public IReadOnlyList<ToneModelRecord> ToneModels { get; }

    public string Name { get; }

    public string Category { get; }

    public string Genre { get; }

    public string Artist { get; }

    public string Song { get; }

    public string Folders { get; }

    public string Author { get; }

    public string Amp { get; }

    public string Cab { get; }

    public string Mics { get; }

    public string Kind { get; }

    public bool HasSettings { get; }

    public bool Favorite { get; }

    public int ModelCount { get; }

    /// <summary>Shown in the grid so a generation 2 library never looks broken.</summary>
    public string SettingsBadge => HasSettings ? "✔" : "—";

    private static string JoinDistinct(
        IReadOnlyList<ToneModelRecord> models,
        Func<ToneModelRecord, string> selector)
        => string.Join(" + ", models
            .Select(selector)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase));

    private static string KindLabel(ToneModelKind kind) => kind switch
    {
        ToneModelKind.Stomp => "stomp",
        ToneModelKind.StompAndAmp => "stomp+amp",
        ToneModelKind.Amp => "amp",
        ToneModelKind.AmpAndCab => "amp+cab",
        ToneModelKind.ComplexRig => "rig",
        ToneModelKind.CustomIR => "ir",
        _ => "",
    };
}
