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
    public void AVoiceThatWroteItsThinkingBeforeItsAnswer_ShowsOnlyTheAnswer()
    {
        var row = new AiOpinionRowViewModel("OpenCode Go");
        row.Complete(new AiOpinion(
            "OpenCode Go",
            "1. Analyze the Request:\n   Let me look at the list...\nBLOC : BOSS MT-2W -> Peavey 5150\nBAFFLE : Mesa Boogie 4x12 OS",
            null,
            4200));

        Assert.StartsWith("BLOC : BOSS MT-2W", row.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("Analyze the Request", row.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AVoiceKeepsItsFullOutput_OneClickAway()
    {
        var row = new AiOpinionRowViewModel("OpenCode Go");
        row.Complete(new AiOpinion(
            "OpenCode Go",
            "1. Analyze the Request:\n   Let me check the list...\nBLOC : BOSS MT-2W -> Peavey 5150\nCONSEIL LIBRE : le vrai matériel est un Mark III.",
            null,
            5000));

        // La réponse nettoyée...
        Assert.DoesNotContain("Analyze the Request", row.Text, StringComparison.Ordinal);

        // ...et la sortie complète, repliée par défaut, au bouton « thinking ».
        Assert.False(row.ShowRawText);
        Assert.True(row.CanShowRaw);
        Assert.Contains("Analyze the Request", row.RawText, StringComparison.Ordinal);

        row.ToggleThinkingCommand.Execute(null);
        Assert.True(row.ShowRawText);

        row.ToggleThinkingCommand.Execute(null);
        Assert.False(row.ShowRawText);
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
