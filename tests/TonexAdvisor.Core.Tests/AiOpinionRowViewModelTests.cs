using TonexAdvisor.App.Services;
using TonexAdvisor.App.ViewModels;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// A voice that fails is ignored, not paraded: a raw error block makes a working advice look
/// broken. The full diagnosis lives in the settings screen instead.
/// </summary>
public class AiOpinionRowViewModelTests
{
    [Fact]
    public void AVoiceInError_IsIgnoredNotDisplayed()
    {
        var row = new AiOpinionRowViewModel(new AiOpinion(
            "Gemini (Google)",
            "",
            "Erreur IA (503) : [{ \"error\": { \"code\": 503, \"message\": \"high demand\" } }]",
            2100));

        Assert.True(row.HasError);
        Assert.Equal("", row.Text);
        Assert.False(row.HasText);
        Assert.Equal("indisponible", row.Status);
    }

    [Fact]
    public void AVoiceThatAnswered_ShowsItsTextAndHowLongItTook()
    {
        var row = new AiOpinionRowViewModel(new AiOpinion("Groq", "Prends le preset 2.", null, 1500));

        Assert.False(row.HasError);
        Assert.Equal("Prends le preset 2.", row.Text);
        Assert.EndsWith(" s", row.Status);
    }
}
