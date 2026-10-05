using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.App.Localization;
using TonexAdvisor.Core.Advice;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.App.ViewModels;

/// <summary>One recommendation of the « Conseils » tab, ready to render.</summary>
public sealed class AdvicePresetRowViewModel
{
    /// <summary>The catalogue's « no genre » sentinel — a value, not a label.</summary>
    private const string GenreSentinelle = "None";

    public AdvicePresetRowViewModel(
        ScoredPreset scored,
        IReadOnlyList<ToneModelRecord> toneModels,
        Action<string> openPreset)
    {
        Scored = scored;
        Key = scored.Preset.Key;

        var preset = scored.Preset;
        Name = preset.Name;
        Category = preset.Category;
        Genre = preset.Genre is GenreSentinelle or "" ? "" : preset.Genre;
        Artist = preset.Artist;
        Song = preset.Song;

        Amp = string.Join(" + ", toneModels
            .Select(model => model.AmpName)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase));
        Cab = string.Join(" + ", toneModels
            .Select(model => model.CabName)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase));

        Score = (int)Math.Round(scored.Score);
        ScoreLabel = Localizer.Instance.Get("Conseil.Score.Pourcent", Score);

        var songLabel = Song.Length > 0 ? Localizer.Instance.Get("Conseil.Presets.Song", Song) : "";
        Meta = string.Join(" · ", new[] { Category, Genre, Amp, Cab, songLabel }
            .Where(value => value.Length > 0));

        // Three reasons are plenty on a card; the rest would be noise.
        Reasons = scored.Reasons.Take(3).Select(reason => reason.Text).ToList();
        ReasonsText = string.Join("  ·  ", Reasons);

        OpenCommand = new RelayCommand(() => openPreset(Key));
    }

    public ScoredPreset Scored { get; }

    public string Key { get; }

    public string Name { get; }

    public string Category { get; }

    public string Genre { get; }

    public string Artist { get; }

    public string Song { get; }

    public string Amp { get; }

    public string Cab { get; }

    public int Score { get; }

    public string ScoreLabel { get; }

    /// <summary>Category · genre · amp · cabinet, whatever is known.</summary>
    public string Meta { get; }

    public IReadOnlyList<string> Reasons { get; }

    /// <summary>The same reasons on a single line, ready for a <c>TextBlock</c>.</summary>
    public string ReasonsText { get; }

    public ICommand OpenCommand { get; }
}
