using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// A reasoning model writes its working before its answer: the screen must show the answer.
/// </summary>
public class AnswerCleanerTests
{
    [Fact]
    public void ThinkingBeforeTheAnswer_IsDropped()
    {
        var text = """
            1. Analyze the Request:
               The user wants a metal tone, let me look at the list...
            BLOC : BOSS MT-2W -> Peavey 5150
            BAFFLE : Mesa Boogie 4x12 OS
            """;

        var cleaned = AnswerCleaner.TrimTo(text, "BLOC :");

        Assert.StartsWith("BLOC : BOSS MT-2W", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("Analyze the Request", cleaned, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAnswerThatStartsAtTheMarker_IsLeftAlone()
    {
        var text = "BLOC : Ibanez TS808 -> Mesa Boogie Triple Rectifier";

        Assert.Equal(text, AnswerCleaner.TrimTo(text, "BLOC :"));
    }

    [Fact]
    public void AModelThatIgnoresTheFormat_IsShownAsItWrote()
    {
        var text = "  Je te conseille le preset ENGL PowerBall, voilà pourquoi.  ";

        Assert.Equal("Je te conseille le preset ENGL PowerBall, voilà pourquoi.", AnswerCleaner.TrimTo(text, "BLOC :"));
    }

    [Fact]
    public void TheMarkerIsFoundCaseInsensitively()
    {
        var cleaned = AnswerCleaner.TrimTo("du bruit puis verdict : le bloc 2", "VERDICT :");

        Assert.Equal("verdict : le bloc 2", cleaned);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NothingToClean_GivesNothingBack(string? text)
    {
        Assert.Equal("", AnswerCleaner.TrimTo(text, "BLOC :"));
    }
}
