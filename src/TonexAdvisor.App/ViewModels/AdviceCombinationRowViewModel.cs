using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.App.Localization;
using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.App.ViewModels;

/// <summary>The recommended amplifier + stomp + cabinet combination.</summary>
public sealed class AdviceCombinationRowViewModel
{
    public AdviceCombinationRowViewModel(ScoredCombination scored, Action<string> openToneModel)
    {
        Scored = scored;
        Key = scored.Example.Key;

        Amp = scored.Amp;
        Stomp = scored.Stomp;
        Cab = scored.Cab;

        Score = (int)Math.Round(scored.Score);
        ScoreLabel = $"{Score} %";

        // Stomp and amplifier come from one and the same capture: they are drawn as a single
        // chain step so nobody reads them as two independent picks.
        Block = scored.Stomp.Length > 0
            ? $"{scored.Stomp}  →  {scored.Amp}"
            : scored.Amp;

        CabLine = scored.Cab.Length > 0
            ? Localizer.Instance.Get("Conseil.Combination.BaffleComplet", scored.Cab, scored.CabNote)
            : Localizer.Instance.Get("Conseil.Combination.BaffleSimple", scored.CabNote);

        PresetCount = scored.PresetCount;
        Usage = PresetCount > 0
            ? Localizer.Instance.Plural(PresetCount, "Conseil.Combination.Uses")
            : Localizer.Instance["Conseil.Combination.Uses.none"];

        Reasons = scored.Reasons.Take(3).Select(reason => reason.Text).ToList();
        ReasonsText = string.Join("  ·  ", Reasons);

        OpenCommand = new RelayCommand(() => openToneModel(Key));
    }

    public ScoredCombination Scored { get; }

    public string Key { get; }

    public string Amp { get; }

    public string Stomp { get; }

    public string Cab { get; }

    /// <summary>Stomp → amplifier: the indivisible captured block.</summary>
    public string Block { get; }

    /// <summary>Cabinet line, always stating that it may be swapped for any other one.</summary>
    public string CabLine { get; }

    public int Score { get; }

    public string ScoreLabel { get; }

    public string Usage { get; }

    /// <summary>How many presets of the library already use this combination.</summary>
    public int PresetCount { get; }

    public IReadOnlyList<string> Reasons { get; }

    /// <summary>The same reasons on a single line, ready for a <c>TextBlock</c>.</summary>
    public string ReasonsText { get; }

    public ICommand OpenCommand { get; }
}
