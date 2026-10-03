using TonexAdvisor.App.Services;
using TonexAdvisor.App.ViewModels;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// A row exists before its voice answers and must say so; a voice that fails stays compact, with
/// its detail one click away.
/// </summary>
public class AiOpinionRowViewModelTests
{
    [Fact]
    public void AVoiceThatHasNotAnsweredYet_SpinsAndSaysSo()
    {
        var row = new AiOpinionRowViewModel("OpenCode Go");

        Assert.Equal("OpenCode Go", row.Provider);
        Assert.True(row.IsPending);
        Assert.Equal("en cours…", row.Status);
        Assert.True(row.ShowPlainStatus);
        Assert.False(row.HasError);
        Assert.False(row.HasText);
    }

    [Fact]
    public void AVoiceInError_IsIgnoredNotParaded_ButItsDetailIsOneClickAway()
    {
        var row = new AiOpinionRowViewModel("Gemini (Google)");
        row.Complete(new AiOpinion(
            "Gemini (Google)",
            "",
            "Erreur IA (503) : [{ \"error\": { \"code\": 503, \"message\": \"high demand\" } }]",
            2100));

        Assert.False(row.IsPending);
        Assert.True(row.HasError);
        Assert.Equal("", row.Text);
        Assert.Equal("indisponible", row.Status);
        Assert.True(row.HasErrorDetail);
        Assert.False(row.IsErrorExpanded, "the detail stays folded");

        row.ToggleErrorCommand.Execute(null);
        Assert.True(row.IsErrorExpanded);
        Assert.Contains("high demand", row.ErrorDetail, StringComparison.Ordinal);

        row.ToggleErrorCommand.Execute(null);
        Assert.False(row.IsErrorExpanded);
    }

    [Fact]
    public void AVoiceThatAnswered_ShowsItsTextAndHowLongItTook()
    {
        var row = new AiOpinionRowViewModel(new AiOpinion("Groq", "Prends le preset 2.", null, 1500));

        Assert.False(row.IsPending);
        Assert.False(row.HasError);
        Assert.Equal("Prends le preset 2.", row.Text);
        Assert.EndsWith(" s", row.Status);
        Assert.False(row.HasErrorDetail);
    }
}
