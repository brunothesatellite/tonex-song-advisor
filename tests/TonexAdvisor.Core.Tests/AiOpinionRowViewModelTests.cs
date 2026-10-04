using TonexAdvisor.App.Services;
using TonexAdvisor.App.ViewModels;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// A row exists before its voice answers; its thinking is hidden unless asked for, and leaves
/// with the generation. A voice that fails stays compact, with its detail one click away.
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
    public void TheThinkingIsHiddenByDefault_AndLeavesWithTheGeneration()
    {
        var row = new AiOpinionRowViewModel("OpenCode Go");
        row.AddThinking(new SseDelta("je réfléchis…", true));

        // Masqué par défaut, bouton présent pendant la génération.
        Assert.False(row.HasThinking);
        Assert.True(row.CanToggleThinking);

        row.ToggleThinkingCommand.Execute(null);
        Assert.True(row.HasThinking);

        row.ToggleThinkingCommand.Execute(null);
        Assert.False(row.HasThinking);

        row.Complete(new AiOpinion("OpenCode Go", "BLOC : X -> Y\nCONSEIL LIBRE : z.", null, 5000));

        // Le résultat est là : le bouton disparaît avec la génération.
        Assert.False(row.CanToggleThinking);
        Assert.False(row.HasThinking);
    }

    [Fact]
    public void TheAnswerIsSplitSoItsMarkersCanBeColoured()
    {
        var row = new AiOpinionRowViewModel("OpenCode Go");
        row.Complete(new AiOpinion(
            "OpenCode Go",
            "BLOC : X -> Y\nRÉGLAGES : Gain 7\nCONSEIL LIBRE : z.",
            null,
            5000));

        Assert.Equal("BLOC :", row.Lines[0].Label);
        Assert.Equal("X -> Y", row.Lines[0].Body);
        Assert.Equal("RÉGLAGES :", row.Lines[1].Label);
        Assert.Equal("CONSEIL LIBRE :", row.Lines[2].Label);
        Assert.Equal("z.", row.Lines[2].Body);
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
