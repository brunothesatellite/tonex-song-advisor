using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.App.Config;
using TonexAdvisor.App.Localization;
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
    private static string DefaultHint =>
        Localizer.Instance["Conseil.Indice.Defaut"];

    /// <summary>
    /// Deadline for one voice. Generous on purpose: a prompt carrying the whole catalogue is
    /// long, and a reasoning model spends its time before answering — cutting it at 60 s turned
    /// every voice into « indisponible ».
    /// </summary>
    private static readonly TimeSpan VoiceTimeout = TimeSpan.FromSeconds(150);

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

    /// <summary>
    /// Ce que contient <see cref="AiText"/> : l'avis d'une seule voix, ou l'avis convergé quand
    /// plusieurs voix se sont exprimées.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiTitle))]
    private string _aiTitle = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiThinking), nameof(CanToggleAiThinking))]
    private string _aiThinking = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiThinking))]
    private bool _isAiThinkingExpanded;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiStatus))]
    private string _aiStatus = "";

    // ── Bascule à chaud (§11.4) ────────────────────────────────────────────
    // Chaque titre d'avis garde son dernier compositeur : la culture change, le texte se
    // rebâtit avec les mêmes arguments — sans jamais rejeter de requête IA pour autant.

    private Func<string> _aiTitleBuilder = static () => "";

    private Func<string> _aiStatusBuilder = static () => "";

    private static string DefaultVoicesTitle() => Localizer.Instance["Conseil.Voix.Titre"];

    private Func<string> _voicesTitleBuilder = DefaultVoicesTitle;

    /// <summary>Fixe un titre d'avis et mémorise son compositeur (§11.4).</summary>
    private void SetAiTitle(Func<string> build)
    {
        _aiTitleBuilder = build;
        AiTitle = build();
    }

    /// <summary>Fixe le statut d'avis et mémorise son compositeur (§11.4).</summary>
    private void SetAiStatus(Func<string> build)
    {
        _aiStatusBuilder = build;
        AiStatus = build();
    }

    /// <summary>Fixe le titre des voix et mémorise son compositeur (§11.4).</summary>
    private void SetVoicesTitle(Func<string> build)
    {
        _voicesTitleBuilder = build;
        VoicesTitle = build();
    }

    /// <summary>Rejoue les trois compositeurs dans la langue courante à chaque bascule.</summary>
    private void RebuildAiTitles()
    {
        VoicesTitle = _voicesTitleBuilder();
        AiTitle = _aiTitleBuilder();
        AiStatus = _aiStatusBuilder();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAiEnabled), nameof(HasAiThinking), nameof(CanToggleAiThinking))]
    private bool _isAiBusy;

    /// <summary>
    /// The arbitre is working: the panel says so instead of leaving a blank where the verdict
    /// will be.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArbitreElapsedLabel))]
    private bool _isArbitreWorking;

    /// <summary>Seconds the arbitre has been working — proof of life while it thinks.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ArbitreElapsedLabel))]
    private int _arbitreElapsed;

    public string ArbitreElapsedLabel =>
        IsArbitreWorking ? Localizer.Instance.Get("Conseil.Arbitre.Ecoule", ArbitreElapsed) : "";

    private readonly DispatcherTimer _ticker = new DispatcherTimer
    {
        Interval = TimeSpan.FromSeconds(1),
    };

    /// <summary>
    /// The answer, split so its markers can be coloured. Rebuilt as it streams, so the markers
    /// are coloured while they appear and not only at the end.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiLines))]
    private IReadOnlyList<AnswerLineViewModel> _aiLines = Array.Empty<AnswerLineViewModel>();

    private int _linesBuiltLength;

    public bool HasAiLines => AiLines.Count > 0;

    private readonly Func<AppConfig> _configLoader;
    private readonly IUserStateStore _stateStore;
    private CancellationTokenSource? _aiCts;

    public AdviceViewModel(LibraryViewModel owner, Func<AppConfig>? configLoader = null, IUserStateStore? stateStore = null)
    {
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _configLoader = configLoader ?? (() => AppConfig.Load());
        _stateStore = stateStore ?? new UserStateStore();

        _ticker.Tick += (_, _) => Tick();
        _ticker.Start();

        // Bascule à chaud (§11.4) : les titres d'avis affichés se rebâtissent dans la langue
        // choisie — pas de nouvelle requête, seuls les libellés changent.
        Localizer.Instance.CultureChanged += RebuildAiTitles;
    }

    /// <summary>
    /// One more second of waiting, for every voice still working and for the arbitre. A stalled
    /// spinner cannot lie about this one.
    /// </summary>
    private void Tick()
    {
        if (!IsAiBusy)
            return;

        foreach (var row in Opinions.Where(row => row.IsPending).ToList())
            row.Tick();

        if (IsArbitreWorking)
            ArbitreElapsed++;
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

    public bool HasAiTitle => AiTitle.Length > 0;

    /// <summary>
    /// Title of the voices block: they propose when an arbitre will decide, they suggest when
    /// nobody will. Rebuilt on every language switch from the stored composer (§11.4).
    /// </summary>
    public string VoicesTitle { get; private set; } = DefaultVoicesTitle();

    /// <summary>
    /// The referee's thinking streams live while it works — hidden unless the user asks for it —
    /// and the « thinking » button leaves with the generation.
    /// </summary>
    public bool HasAiThinking => IsAiThinkingExpanded && AiThinking.Length > 0;

    /// <summary>The « thinking » button of the verdict lives as long as the generation does.</summary>
    public bool CanToggleAiThinking => IsAiBusy && AiThinking.Length > 0;

    [RelayCommand]
    private void ToggleAiThinking()
        => IsAiThinkingExpanded = !IsAiThinkingExpanded;

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
        SetAiStatus(static () => "");
        SetAiTitle(static () => "");
        ArbitreElapsed = 0;
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
            ErrorMessage = Localizer.Instance["Conseil.Erreur.AucuneBiblio"];
            return;
        }

        var query = AdviceQuery.Create(Artist, Song, Style);
        if (query.IsBlank)
        {
            ErrorMessage = Localizer.Instance["Conseil.Erreur.RequeteVide"];
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
        SetAiStatus(static () => "");
        SetAiTitle(static () => "");
        ArbitreElapsed = 0;
        Opinions.Clear();
        OnPropertyChanged(nameof(HasOpinions));

        var query = AdviceQuery.Create(Artist, Song, Style);
        if (query.IsBlank)
        {
            SetAiStatus(() => Localizer.Instance["Conseil.Erreur.DecrireDabord"]);
            return;
        }

        // Le classement local reste affiché, mais le contexte envoyé aux IA est le catalogue
        // complet de la bibliothèque : c'est là qu'elles peuvent choisir ce que la correspondance
        // locale ignore (« un son Slash » → « AFD100 »).
        if (!HasPresets && Combination is null)
            Advise();

        var config = _configLoader();
        var state = _stateStore.Load();
        var voices = AiConsultation.BuildVoices(config, state);
        var arbitre = AiConsultation.BuildArbitre(config);

        SetVoicesTitle(() => arbitre is null
            ? Localizer.Instance["Conseil.Voix.Suggestions"]
            : Localizer.Instance["Conseil.Voix.Titre"]);

        if (voices.Count == 0 && arbitre is null)
        {
            SetAiStatus(() => Localizer.Instance["Conseil.Erreur.SansCle"]);
            return;
        }

        var prompt = CataloguePrompt.Build(query, _owner.Index!, Localizer.Instance.Culture);

        _aiCts?.Cancel();
        _aiCts = new CancellationTokenSource();

        // Chaque voix a sa ligne dès la première seconde : le panneau ne change pas de forme
        // pendant l'attente, il se remplit.
        foreach (var voice in voices)
            Opinions.Add(new AiOpinionRowViewModel(voice.Label));
        OnPropertyChanged(nameof(HasOpinions));

        IsAiBusy = true;
        try
        {
            // Aucune voix : l'arbitre parle seul, à partir du prompt complet.
            if (voices.Count == 0)
            {
                IsArbitreWorking = true;
                var alone = await arbitre!.Client.AskStreamAsync(
                    arbitre.Model,
                    prompt,
                    CataloguePrompt.MaxTokens,
                    delta => Dispatch(() =>
                    {
                        // Tout le flux est la réflexion de l'arbitre : dépliable au bouton.
                        // La réponse, elle, s'écrit aussi dans AiText, puis se nettoie à la fin.
                        AiThinking += delta.Text;

                        if (!delta.IsReasoning)
                        {
                            AiText += delta.Text;

                            if (AiText.Length - _linesBuiltLength > 120)
                                RebuildAiLines();
                        }
                    }),
                    _aiCts.Token);

                if (AiText.Length == 0 && alone.Length > 0)
                    AiText = alone;

                AiText = AnswerFormat.ForCulture(Localizer.Instance.Culture).ExtractVoice(AiText);
                RebuildAiLines();
                SetAiTitle(() => Localizer.Instance.Get("Conseil.Reponse.Suggestion", arbitre.Model));
                SetAiStatus(() => Localizer.Instance.Get("Conseil.Avis.Statut", arbitre.Model));
                return;
            }

            // Des voix : chacune propose dans sa ligne, au fil de l'eau.
            var opinions = await AiConsultation.ConsultAsync(
                voices,
                prompt,
                CataloguePrompt.MaxTokens,
                onDelta: (provider, delta) => Dispatch(() => AppendThinking(provider, delta)),
                onOpinion: opinion => Dispatch(() => AppendOpinion(opinion)),
                cancellationToken: _aiCts.Token);

            var usable = opinions.Where(opinion => opinion.Ok).ToList();

            if (usable.Count == 0)
            {
                SetAiStatus(() => Localizer.Instance.Get("Conseil.Avis.Indisponibles",
                    string.Join(", ", opinions.Select(opinion => opinion.Provider))));
                return;
            }

            // Pas d'arbitre (clé OpenCode absente) : les voix restent des suggestions.
            if (arbitre is null)
            {
                SetAiStatus(() => Localizer.Instance.Plural(usable.Count, "Conseil.Avis.SansArbitre"));
                return;
            }

            // L'arbitre tranche, même quand une seule voix s'est exprimée.
            var arbitration = ArbitrationPrompt.Build(
                query,
                usable.Select(opinion => new Opinion(opinion.Provider, opinion.Text)).ToList(),
                _owner.Index!,
                Localizer.Instance.Culture);

            IsArbitreWorking = true;
            var text = await arbitre.Client.AskStreamAsync(
                arbitre.Model,
                arbitration,
                ArbitrationPrompt.MaxTokens,
                delta => Dispatch(() =>
                {
                    // Idem : tout le flux est la réflexion, la réponse arrive en même temps.
                    AiThinking += delta.Text;

                    if (!delta.IsReasoning)
                        AiText += delta.Text;
                }),
                _aiCts.Token);

            if (AiText.Length == 0 && text.Length > 0)
                AiText = text;

            // L'arbitre écrit son travail (évaluation des avis, brouillons) dans sa sortie : on
            // ne garde que son verdict, du marqueur à la fin du CONSEIL LIBRE.
            AiText = AnswerFormat.ForCulture(Localizer.Instance.Culture).ExtractVerdict(AiText);
            RebuildAiLines();
            SetAiTitle(() => Localizer.Instance.Get("Conseil.Reponse.Verdict", arbitre.Model));

            SetAiStatus(() => AiText.Length > 0
                ? Localizer.Instance.Get("Conseil.Avis.Verdict.Sur", usable.Count,
                    string.Join(", ", usable.Select(opinion => opinion.Provider)))
                : Localizer.Instance["Conseil.Avis.Verdict.Vide"]);
        }
        catch (OperationCanceledException)
        {
            SetAiStatus(() => Localizer.Instance["Conseil.Erreur.Annule"]);
        }
        catch (Exception exception)
        {
            SetAiStatus(() => Localizer.Instance.Get("Conseil.Erreur.Suite", exception.Message));
        }
        finally
        {
            // Une voix jamais revenue ne doit pas rester à tourner.
            foreach (var row in Opinions.Where(candidate => candidate.IsPending).ToList())
                row.Complete(new AiOpinion(row.Provider, "", Localizer.Instance["Conseil.Avis.Interrompu"], 0));

            IsArbitreWorking = false;
            IsAiBusy = false;
        }
    }

    [RelayCommand]
    private void CancelAi() => _aiCts?.Cancel();

    /// <summary>
    /// Fait défiler la réflexion d'une voix dans sa propre ligne, en direct : on voit le modèle
    /// travailler, puis sa ligne se réduit à la réponse quand il a fini.
    /// </summary>
    private void AppendThinking(string provider, SseDelta delta)
    {
        var row = Opinions.FirstOrDefault(candidate =>
            string.Equals(candidate.Provider, provider, StringComparison.Ordinal));

        row?.AddThinking(delta);
    }

    /// <summary>
    /// Rebuilds the coloured lines of the answer. Called while it streams, throttled: the
    /// markers must be coloured as they appear, not only once the answer is complete.
    /// </summary>
    private void RebuildAiLines()
    {
        AiLines = AnswerLineViewModel.Split(AiText);
        _linesBuiltLength = AiText.Length;
    }

    /// <summary>
    /// Complète la ligne d'une voix depuis le thread réseau : la ligne existe déjà (elle tourne),
    /// il lui suffit d'apprendre ce que la voix a répondu.
    /// </summary>
    private void AppendOpinion(AiOpinion opinion)
    {
        var row = Opinions.FirstOrDefault(candidate =>
            string.Equals(candidate.Provider, opinion.Provider, StringComparison.Ordinal));

        if (row is not null)
        {
            row.Complete(opinion);
            return;
        }

        // Voix inattendue : insérée à sa place pour garder l'ordre du panneau.
        var rank = OpinionOrder.Rank(opinion.Provider);
        var index = 0;

        while (index < Opinions.Count && OpinionOrder.Rank(Opinions[index].Provider) <= rank)
            index++;

        Opinions.Insert(index, new AiOpinionRowViewModel(opinion));
        OnPropertyChanged(nameof(HasOpinions));
    }

    /// <summary>The stream arrives on a network thread: the UI updates from its own.</summary>
    private static void Dispatch(Action action)
        => Dispatcher.UIThread.Post(action);

    private static string BuildHint(int presets, int combinations)
    {
        if (presets == 0 && combinations == 0)
            return Localizer.Instance["Conseil.Indice.Vide"];

        if (presets == 0)
            return Localizer.Instance["Conseil.Indice.AucunPreset"];

        return combinations > 0
            ? Localizer.Instance.Plural(presets, "Conseil.Indice.Combinaison")
            : Localizer.Instance.Plural(presets, "Conseil.Indice.PresetSeul");
    }
}
