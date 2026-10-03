using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.App.Config;
using TonexAdvisor.App.Services;
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

    // ── Conseil IA (Phase 4) ───────────────────────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiText), nameof(HasAiThinking))]
    private string _aiText = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiThinking))]
    private string _aiThinking = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiStatus))]
    private string _aiStatus = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAiEnabled), nameof(HasAiThinking))]
    private bool _isAiBusy;

    private readonly Func<AppConfig> _configLoader;
    private CancellationTokenSource? _aiCts;

    public AdviceViewModel(LibraryViewModel owner, Func<AppConfig>? configLoader = null)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _configLoader = configLoader ?? (() => AppConfig.Load());
    }

    public ObservableCollection<AdvicePresetRowViewModel> Presets { get; } = new();

    public bool HasError => ErrorMessage.Length > 0;

    public bool HasPresets => Presets.Count > 0;

    public bool HasCombination => Combination is not null;

    // ── Conseil IA ────────────────────────────────────────────────────────

    /// <summary>La réponse de l'IA, remplie au fil du flux.</summary>
    public bool HasAiText => AiText.Length > 0;

    /// <summary>
    /// La chaîne de pensée n'est montrée que pendant la génération : une fois la réponse là, ce
    /// n'est plus que du bruit pour l'utilisateur.
    /// </summary>
    public bool HasAiThinking => AiThinking.Length > 0 && (IsAiBusy || AiText.Length == 0);

    public bool HasAiStatus => AiStatus.Length > 0;

    /// <summary>Le bouton « Demander à l'IA » est actif hors requête.</summary>
    public bool IsAiEnabled => !IsAiBusy;

    /// <summary>Empties the previous answer, called whenever a new library is opened.</summary>
    public void Reset()
    {
        Presets.Clear();
        Combination = null;
        HasRun = false;
        ErrorMessage = "";
        Hint = DefaultHint;
        AiText = "";
        AiThinking = "";
        AiStatus = "";
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

    /// <summary>
    /// Demande un conseil IA à propos de la sélection locale. En cas d'échec — hors-ligne, clé
    /// invalide, quota dépassé — le classement local reste affiché : l'IA est un plus, jamais la
    /// seule source de conseil.
    /// </summary>
    [RelayCommand]
    private async Task AskAiAsync()
    {
        if (IsAiBusy)
            return;

        AiText = "";
        AiThinking = "";
        AiStatus = "";

        var query = AdviceQuery.Create(Artist, Song, Style);
        if (query.IsBlank)
        {
            AiStatus = "Décrivez d'abord la chanson : un artiste, un titre ou un style.";
            return;
        }

        // Le classement local d'abord : c'est lui qui alimente le contexte envoyé au modèle.
        if (!HasPresets && Combination is null)
            Advise();

        if (!HasPresets && Combination is null)
        {
            AiStatus = "Rien à soumettre à l'IA : aucun preset de cette bibliothèque ne correspond.";
            return;
        }

        var config = _configLoader();
        if (!config.HasApiKey)
        {
            AiStatus = "Clé API absente : ouvre Réglages → Conseil IA pour l'enregistrer. " +
                       "Le classement local reste valable.";
            return;
        }

        var prompt = AdvicePrompt.Build(
            query,
            Presets.Select(row => row.Scored).ToList(),
            Combination?.Scored,
            _owner.Index);

        _aiCts?.Cancel();
        _aiCts = new CancellationTokenSource();

        IsAiBusy = true;
        try
        {
            var client = new OpenCodeClient(config);

            var text = await client.AskStreamAsync(
                config.Model,
                prompt,
                AdvicePrompt.MaxTokens,
                delta => Dispatch(() =>
                {
                    if (delta.IsReasoning)
                        AiThinking += delta.Text;
                    else
                        AiText += delta.Text;
                }),
                _aiCts.Token);

            // Modèle qui n'a produit que sa chaîne de pensée : on l'affiche, mais en le signalant,
            // car c'est rarement la réponse attendue.
            if (AiText.Length == 0 && text.Length > 0)
            {
                AiText = text;
                AiStatus = "Le modèle n'a renvoyé que sa réflexion (limite de tokens atteinte) : " +
                           "relance pour avoir la réponse finale.";
            }
            else
            {
                AiStatus = AiText.Length > 0
                    ? $"Conseil IA — {OpenCodeModels.Resolve(config.Model).Label}"
                    : "L'IA n'a rien renvoyé : le classement local reste la référence.";
            }
        }
        catch (OperationCanceledException)
        {
            AiStatus = "Conseil IA annulé.";
        }
        catch (Exception exception)
        {
            AiStatus = $"{exception.Message} — le classement local reste affiché.";
        }
        finally
        {
            IsAiBusy = false;
        }
    }

    [RelayCommand]
    private void CancelAi() => _aiCts?.Cancel();

    /// <summary>Le flux arrive d'un thread réseau : l'IU se met à jour depuis le sien.</summary>
    private static void Dispatch(Action action)
        => Dispatcher.UIThread.Post(action);

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
