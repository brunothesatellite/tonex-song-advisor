using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.App.Services;
using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.App.ViewModels;

/// <summary>
/// One voice of the crossed panel, ready to render.
/// </summary>
/// <remarks>
/// A row exists <b>before</b> its voice answers: it spins while the voice works, so the panel
/// reads the same from the first second — reference first, then the challengers. When a voice
/// fails, its row stays compact (« indisponible ») and the detail unfolds on a click: a raw error
/// block makes a working advice look broken, but hiding it completely hides the diagnosis.
/// </remarks>
public partial class AiOpinionRowViewModel : ViewModelBase
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrorDetail), nameof(ShowPlainStatus))]
    private bool _hasError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPlainStatus), nameof(HasThinking))]
    private bool _isPending = true;

    [ObservableProperty]
    private string _provider = "";

    [ObservableProperty]
    private string _status = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasText))]
    private string _text = "";

    /// <summary>Ce que le modèle est en train d'écrire — son travail, pas sa réponse.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThinking))]
    private string _thinking = "";

    [ObservableProperty]
    private string _errorDetail = "";

    [ObservableProperty]
    private bool _isErrorExpanded;

    /// <summary>A voice that has not answered yet.</summary>
    public AiOpinionRowViewModel(string provider)
    {
        Provider = provider;
        Status = "en cours…";
    }

    /// <summary>A voice that has already answered.</summary>
    public AiOpinionRowViewModel(AiOpinion opinion)
        : this(opinion.Provider)
        => Complete(opinion);

    public bool HasText => Text.Length > 0;

    /// <summary>La réflexion s'affiche pendant qu'elle s'écrit, puis laisse la réponse seule.</summary>
    public bool HasThinking => IsPending && Thinking.Length > 0;

    /// <summary>True when the row carries a message worth unfolding.</summary>
    public bool HasErrorDetail => HasError && ErrorDetail.Length > 0;

    /// <summary>Plain status (working, or the time it took) — no click needed there.</summary>
    public bool ShowPlainStatus => !HasError;

    /// <summary>Ajoute un fragment de la réflexion en cours, tant que la voix n'a pas répondu.</summary>
    public void AddThinking(SseDelta delta)
    {
        if (IsPending)
            Thinking += delta.Text;
    }

    /// <summary>Replaces the spinning row with what the voice finally said.</summary>
    public void Complete(AiOpinion opinion)
    {
        IsPending = false;
        HasError = opinion.Error is not null;
        Status = HasError ? "indisponible" : $"{opinion.ElapsedMs / 1000d:0.0} s";

        // Un modèle raisonneur écrit son travail dans sa réponse : brouillons, vérifications,
        // comptages. On ne garde que le bloc de réponse, du marqueur de format à la fin du
        // CONSEIL LIBRE.
        Text = HasError ? "" : AnswerCleaner.Extract(opinion.Text, "BLOC :", "CONSEIL LIBRE");
        ErrorDetail = opinion.Error ?? "";
        IsErrorExpanded = false;
    }

    [RelayCommand]
    private void ToggleError()
        => IsErrorExpanded = !IsErrorExpanded;
}
