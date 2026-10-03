using TonexAdvisor.App.Services;

namespace TonexAdvisor.App.ViewModels;

/// <summary>One voice of the crossed panel, ready to render.</summary>
public sealed class AiOpinionRowViewModel
{
    public AiOpinionRowViewModel(AiOpinion opinion)
    {
        Opinion = opinion;

        Provider = opinion.Provider;
        HasError = opinion.Error is not null;

        Status = HasError
            ? opinion.Error ?? ""
            : $"{opinion.ElapsedMs / 1000d:0.0} s";

        Text = opinion.Text;
    }

    public AiOpinion Opinion { get; }

    public string Provider { get; }

    /// <summary>Error message, or the time the voice took.</summary>
    public string Status { get; }

    public bool HasError { get; }

    public string Text { get; }

    /// <summary>True when the card has more than a status line to show.</summary>
    public bool HasText => Text.Length > 0;
}
