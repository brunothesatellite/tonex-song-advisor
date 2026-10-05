using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.App.Localization;
using TonexAdvisor.App.Services;
using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.App.ViewModels;

/// <summary>
/// One voice of the crossed panel, ready to render.
/// </summary>
/// <remarks>
/// A row exists <b>before</b> its voice answers: it spins while the voice works. Its thinking is
/// hidden by default — a long chain of thought is noise on screen — and the « thinking » button
/// shows it live, then disappears with the generation. When the answer arrives, the row keeps
/// only the answer, with its format markers coloured. A voice that fails is compact
/// (« indisponible ») with its detail one click away.
/// </remarks>
public partial class AiOpinionRowViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorDetail), nameof(ShowPlainStatus))]
    private bool _hasError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlainStatus), nameof(CanToggleThinking), nameof(ElapsedLabel))]
    private bool _isPending = true;

    /// <summary>Seconds spent waiting — a sign of life no stalled animation can fake.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ElapsedLabel))]
    private int _elapsed;

    /// <summary>Shown while the voice works.</summary>
    public string ElapsedLabel => IsPending ? Localizer.Instance.Get("Voix.Duree", Elapsed) : "";

    [ObservableProperty]
    private string _provider = "";

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasText), nameof(Lines))]
    private string _text = "";

    /// <summary>What the model is writing: its working, not its answer.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThinking), nameof(CanToggleThinking))]
    private string _thinking = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThinking))]
    private bool _isThinkingExpanded;

    [ObservableProperty]
    private string _errorDetail = "";

    [ObservableProperty]
    private bool _isErrorExpanded;

    /// <summary>A voice that has not answered yet.</summary>
    public AiOpinionRowViewModel(string provider)
    {
        Provider = provider;
        Status = Localizer.Instance["Conseil.EnCours"];
    }

    /// <summary>A voice that has already answered.</summary>
    public AiOpinionRowViewModel(AiOpinion opinion)
        : this(opinion.Provider)
        => Complete(opinion);

    public bool HasText => Text.Length > 0;

    /// <summary>The answer, split so its markers can be coloured.</summary>
    public IReadOnlyList<AnswerLineViewModel> Lines => AnswerLineViewModel.Split(Text);

    /// <summary>The working, shown only while the user asks for it.</summary>
    public bool HasThinking => IsThinkingExpanded && Thinking.Length > 0;

    /// <summary>The « thinking » button lives as long as the generation does.</summary>
    public bool CanToggleThinking => IsPending && Thinking.Length > 0;

    /// <summary>True when the row carries a message worth unfolding.</summary>
    public bool HasErrorDetail => HasError && ErrorDetail.Length > 0;

    /// <summary>Plain status (working, or the time it took) — no click needed there.</summary>
    public bool ShowPlainStatus => !HasError;

    /// <summary>Adds a fragment of the thinking, as long as the voice has not answered.</summary>
    public void AddThinking(SseDelta delta)
    {
        if (IsPending)
            Thinking += delta.Text;
    }

    /// <summary>One more second of waiting.</summary>
    public void Tick()
    {
        if (IsPending)
            Elapsed++;
    }

    /// <summary>Replaces the spinning row with what the voice finally said.</summary>
    public void Complete(AiOpinion opinion)
    {
        IsPending = false;
        HasError = opinion.Error is not null;
        Status = HasError
            ? Localizer.Instance["Conseil.Avis.Indispo"]
            : Localizer.Instance.Get("Conseil.Avis.Ecoule", opinion.ElapsedMs / 1000d);

        // Un modèle raisonneur écrit son travail dans sa réponse : brouillons, vérifications,
        // comptages. On ne garde que le bloc de réponse, du marqueur de format à la fin du
        // CONSEIL LIBRE.
        var format = AnswerFormat.ForCulture(Localizer.Instance.Culture);
        Text = HasError ? "" : format.ExtractVoice(opinion.Text);
        ErrorDetail = opinion.Error ?? "";
        IsErrorExpanded = false;
        IsThinkingExpanded = false;
    }

    [RelayCommand]
    private void ToggleError()
        => IsErrorExpanded = !IsErrorExpanded;

    [RelayCommand]
    private void ToggleThinking()
        => IsThinkingExpanded = !IsThinkingExpanded;
}
