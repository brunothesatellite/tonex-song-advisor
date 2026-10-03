using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.App.ViewModels;

/// <summary>
/// The « Conseils » tab: asks for a song, an artist or a style and ranks the library locally.
/// </summary>
/// <remarks>
/// Scoring happens in <see cref="LibraryAdvisor"/>, in milliseconds and without any network
/// call, so this tab is always usable — even with no API key configured. The AI of phase 4 will
/// be fed this shortlist rather than the whole library.
/// </remarks>
public partial class AdviceViewModel : ViewModelBase
{
    private const int PresetCount = 3;
    private const int CombinationCount = 1;

    private readonly LibraryViewModel _owner;

    [ObservableProperty]
    private string _artist = "";

    [ObservableProperty]
    private string _song = "";

    [ObservableProperty]
    private string _style = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    [ObservableProperty]
    private string _hint = DefaultHint;

    /// <summary>Shown before the tab has been asked anything.</summary>
    private const string DefaultHint =
        "Décrivez la chanson : un artiste, un titre, ou simplement l'ambiance recherchée " +
        "(« metal », « blues », « clean funk »).";

    [ObservableProperty]
    private bool _hasRun;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCombination))]
    private AdviceCombinationRowViewModel? _combination;

    public AdviceViewModel(LibraryViewModel owner)
        => _owner = owner ?? throw new ArgumentNullException(nameof(owner));

    public ObservableCollection<AdvicePresetRowViewModel> Presets { get; } = new();

    public bool HasError => ErrorMessage.Length > 0;

    public bool HasPresets => Presets.Count > 0;

    public bool HasCombination => Combination is not null;

    /// <summary>Empties the previous answer, called whenever a new library is opened.</summary>
    public void Reset()
    {
        Presets.Clear();
        Combination = null;
        HasRun = false;
        ErrorMessage = "";
        Hint = DefaultHint;
        OnPropertyChanged(nameof(HasPresets));
    }

    [RelayCommand]
    private void Advise()
    {
        ErrorMessage = "";
        Presets.Clear();
        Combination = null;
        HasRun = true;
        OnPropertyChanged(nameof(HasPresets));

        var index = _owner.Index;
        if (index is null)
        {
            ErrorMessage = "Aucune bibliothèque chargée.";
            return;
        }

        var query = AdviceQuery.Create(Artist, Song, Style);
        if (query.IsBlank)
        {
            ErrorMessage = "Indiquez au moins un artiste, une chanson ou un style.";
            Hint = "";
            return;
        }

        var advisor = new LibraryAdvisor(index);

        foreach (var scored in advisor.RankPresets(query, PresetCount))
        {
            Presets.Add(new AdvicePresetRowViewModel(
                scored,
                index.ModelsFor(scored.Preset),
                _owner.OpenPresetByKey));
        }

        var combinations = advisor.RankCombinations(query, CombinationCount);
        Combination = combinations.Count > 0
            ? new AdviceCombinationRowViewModel(combinations[0], _owner.OpenToneModelByKey)
            : null;

        OnPropertyChanged(nameof(HasPresets));
        Hint = BuildHint(Presets.Count, combinations.Count);
    }

    private static string BuildHint(int presets, int combinations)
    {
        if (presets == 0 && combinations == 0)
            return "Rien dans cette bibliothèque ne correspond à cette recherche. Essayez un mot-clé " +
                   "plus court, ou cherchez d'abord ce titre dans l'onglet Presets.";

        if (presets == 0)
            return "Aucun preset ne correspond, mais une combinaison d'ampli mérite le détour.";

        return combinations > 0
            ? $"{presets} preset(s) classé(s) et un bloc capturé (stomp + ampli) avec son baffle à tester en premier."
            : $"{presets} preset(s) classé(s) : le premier est le plus proche de votre demande.";
    }
}
