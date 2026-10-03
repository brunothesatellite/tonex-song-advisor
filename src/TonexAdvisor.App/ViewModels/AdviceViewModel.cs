using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.App.Config;
using TonexAdvisor.App.Services;
using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.App.ViewModels;

/// <summary>
/// The « Conseils » tab: a local ranking first, then the opinion of every AI voice the user
/// configured, then an arbitration between them.
/// </summary>
/// <remarks>
/// Scoring happens in <see cref="LibraryAdvisor"/>, in milliseconds and without any network
/// call, so this tab is always usable — even with no key at all. The AIs only ever see this
/// shortlist, they are asked to contradict each other, and the local ranking stays on screen
/// whenever they fail.
/// </remarks>
public partial class AdviceViewModel : ViewModelBase
{
    private const int PresetCount = 3;
    private const int CombinationCount = 1;

    /// <summary>Shown before the tab has been asked anything.</summary>
    private const string DefaultHint =
        "Décrivez la chanson : un artiste, un titre, ou simplement l'ambiance recherchée " +
        "(« metal », « blues », « clean funk »).";

    /// <summary>Deadline for one voice: a slow provider never holds up the others.</summary>
    private static readonly TimeSpan VoiceTimeout = TimeSpan.FromSeconds(60);

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

    [ObservableProperty]
    private bool _hasRun;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCombination))]
    private AdviceCombinationRowViewModel? _combination;

    // ── Avis croisés ──────────────────────────────────────────────────────

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

    /// <summary>One entry per voice consulted, in the order the answers arrived.</summary>
    public ObservableCollection<AiOpinionRowViewModel> Opinions { get; } = new();

    public bool HasError => ErrorMessage.Length > 0;

    public bool HasPresets => Presets.Count > 0;

    public bool HasCombination => Combination is not null;

    // ── Avis croisés ──────────────────────────────────────────────────────

    /// <summary>The answer: the arbitration when several voices spoke, their opinion otherwise.</summary>
    public bool HasAiText => AiText.Length > 0;

    /// <summary>
    /// The chain of thought is shown while generating only: once the answer is there, it is
    /// noise for the user.
    /// </summary>
    public bool HasAiThinking => AiThinking.Length > 0 && (IsAiBusy || AiText.Length == 0);

    public bool HasAiStatus => AiStatus.Length > 0;

    public bool HasOpinions => Opinions.Count > 0;

    /// <summary>The « Demander à l'IA » button is active outside a request.</summary>
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
        Opinions.Clear();
        OnPropertyChanged(nameof(HasPresets));
        OnPropertyChanged(nameof(HasOpinions));
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
    /// Asks every configured voice, then has them contradicted and arbitrated. On any failure —
    /// missing key, offline, quota, timeout — a message explains it and the local ranking stays
    /// on screen: the AI is a bonus, never the only source of advice.
    /// </summary>
    [RelayCommand]
    private async Task AskAiAsync()
    {
        if (IsAiBusy)
            return;

        AiText = "";
        AiThinking = "";
        AiStatus = "";
        Opinions.Clear();
        OnPropertyChanged(nameof(HasOpinions));

        var query = AdviceQuery.Create(Artist, Song, Style);
        if (query.IsBlank)
        {
            AiStatus = "Décrivez d'abord la chanson : un artiste, un titre ou un style.";
            return;
        }

        // The local ranking first: it is what the voices are given to work with.
        if (!HasPresets && Combination is null)
            Advise();

        if (!HasPresets && Combination is null)
        {
            AiStatus = "Rien à soumettre à l'IA : aucun preset de cette bibliothèque ne correspond.";
            return;
        }

        var config = _configLoader();
        var clients = AiConsultation.BuildClients(config);
        if (clients.Count == 0)
        {
            AiStatus = "Clé API absente : renseigne au moins une voix (OpenCode, Gemini, Mistral " +
                       "ou Groq) dans Réglages → Bases & réglages. Le classement local reste valable.";
            return;
        }

        var shortlist = Presets.Select(row => row.Scored).ToList();
        var prompt = AdvicePrompt.Build(query, shortlist, Combination?.Scored, _owner.Index);

        _aiCts?.Cancel();
        _aiCts = new CancellationTokenSource();

        IsAiBusy = true;
        try
        {
            // 1. Every voice at once, each opinion appearing as it lands.
            var opinions = await AiConsultation.ConsultAsync(
                clients,
                client => AiConsultation.ModelFor(config, client.Provider),
                prompt,
                AdvicePrompt.MaxTokens,
                VoiceTimeout,
                onOpinion: opinion => Dispatch(() => AppendOpinion(opinion)),
                cancellationToken: _aiCts.Token);

            var usable = opinions.Where(opinion => opinion.Ok).ToList();

            if (usable.Count == 0)
            {
                // Aucune voix disponible : on le dit sans détailler les erreurs, qui n'ajoutent
                // rien au conseil et donnent l'impression d'un blocage général.
                var names = string.Join(", ", opinions.Select(opinion => opinion.Provider));
                AiStatus = $"Voix indisponibles ({names}) — le classement local reste affiché.";
                return;
            }

            // 2. A single voice has nothing to contradict: its opinion is the answer.
            if (usable.Count == 1)
            {
                AiText = usable[0].Text;
                AiStatus = $"Conseil IA — {usable[0].Provider}";
                return;
            }

            // 3. Several voices: a referee confronts them and ranks the three best proposals.
            var arbitration = ArbitrationPrompt.Build(
                query,
                shortlist,
                Combination?.Scored,
                usable.Select(opinion => new Opinion(opinion.Provider, opinion.Text)).ToList(),
                _owner.Index);

            var referee = clients[0];
            var text = await referee.AskStreamAsync(
                AiConsultation.ModelFor(config, referee.Provider),
                arbitration,
                ArbitrationPrompt.MaxTokens,
                delta => Dispatch(() =>
                {
                    if (delta.IsReasoning)
                        AiThinking += delta.Text;
                    else
                        AiText += delta.Text;
                }),
                _aiCts.Token);

            if (AiText.Length == 0 && text.Length > 0)
                AiText = text;

            AiStatus = AiText.Length > 0
                ? $"Synthèse de {usable.Count} avis ({string.Join(", ", usable.Select(opinion => opinion.Provider))})"
                : "L'arbitre n'a rien renvoyé : les avis restent affichés.";
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

    /// <summary>Adds one voice to the panel, from the network thread.</summary>
    private void AppendOpinion(AiOpinion opinion)
    {
        Opinions.Add(new AiOpinionRowViewModel(opinion));
        OnPropertyChanged(nameof(HasOpinions));
    }

    /// <summary>The stream arrives on a network thread: the UI updates from its own.</summary>
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
