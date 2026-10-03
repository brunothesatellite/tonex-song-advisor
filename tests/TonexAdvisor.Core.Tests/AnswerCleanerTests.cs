using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// A reasoning model writes its working before, around and after its answer: the screen must show
/// the conclusion only.
/// </summary>
public class AnswerCleanerTests
{
    private const string Start = "BLOC :";
    private const string End = "CONSEIL LIBRE";

    [Fact]
    public void WorkingBeforeAndAfterTheAnswer_IsDropped()
    {
        // Ce qu'écrit réellement OpenCode : un brouillon, des vérifications, un recomptage, puis
        // la réponse — et du commentaire après.
        var text = """
            BLOC : Ibanez Tube_Screamer -> Mesa Boogie Mark III
               * Sentence 2: BAFFLE : Mesa 4x12 Celestion V30
            7. **Check Constraints:**
               * French? Yes.
            8. **Final Polish:**
            BLOC : Ibanez Tube_Screamer -> Mesa Boogie Mark III
            BAFFLE : Mesa 4x12 Celestion V30
            RÉGLAGES : Gain 7.5, Mids 3, Treble 7
            ALTERNATIVE : Ibanez TS9 -> Marshall JCM 800
            CONSEIL LIBRE : Pour le vrai son de "Amen", je prends un Mesa Boogie Mark III.

            Let's count sentences:
            1. BLOC : ...
            Total 10 sentences. Fits the 8-12 range perfectly.
            """;

        var cleaned = AnswerCleaner.Extract(text, Start, End);

        Assert.StartsWith("BLOC : Ibanez Tube_Screamer", cleaned, StringComparison.Ordinal);
        Assert.Contains("CONSEIL LIBRE : Pour le vrai son", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("Check Constraints", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("Final Polish", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("Let's count sentences", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("Total 10 sentences", cleaned, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLastVersionOfTheAnswerWins_NotTheFirstDraft()
    {
        var text = """
            BLOC : premier brouillon
            CONSEIL LIBRE : brouillon

            BLOC : réponse finale -> ampli
            BAFFLE : baffle final
            CONSEIL LIBRE : la vraie conclusion
            """;

        var cleaned = AnswerCleaner.Extract(text, Start, End);

        Assert.StartsWith("BLOC : réponse finale", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("premier brouillon", cleaned, StringComparison.Ordinal);
    }

    [Fact]
    public void AProperAnswer_IsLeftWhole()
    {
        var text = """
            BLOC : Ibanez TS808 -> Mesa Boogie Triple Rectifier
            BAFFLE : Mesa Boogie 4x12 Celestion V30
            RÉGLAGES : Gain 7, Bass +2
            ALTERNATIVE : Marshall Super Lead '68
            CONSEIL LIBRE : En studio j'utiliserais une Mesa Dual Rectifier 100W.
            """;

        Assert.Equal(text, AnswerCleaner.Extract(text, Start, End));
    }

    [Fact]
    public void AModelThatIgnoresTheFormat_IsShownAsItWrote()
    {
        var text = "  Je te conseille le preset ENGL PowerBall, voilà pourquoi.  ";

        Assert.Equal("Je te conseille le preset ENGL PowerBall, voilà pourquoi.", AnswerCleaner.Extract(text, Start, End));
    }

    [Fact]
    public void TheArbitrationWorksTheSameWay_FromVerdictToFreeAdvice()
    {
        var text = """
            VERDICT : les 3 meilleures propositions classées — pour chacune : le bloc, le baffle.
            1. Since there are only 2 external opinions, I will rank them.
            VERDICT : 1. Ibanez Tube_Screamer -> Mesa Boogie Mark III (consensus 1/3).
            CONSEIL LIBRE : le matériel réel du morceau est un Mark III.

            Draft the Verdict Section:
            * Must start with "VERDICT : ".
            """;

        var cleaned = AnswerCleaner.Extract(text, "VERDICT :", End);

        Assert.StartsWith("VERDICT : 1. Ibanez Tube_Screamer", cleaned, StringComparison.Ordinal);
        Assert.Contains("CONSEIL LIBRE : le matériel réel", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("I will rank them", cleaned, StringComparison.Ordinal);
        Assert.DoesNotContain("Draft the Verdict", cleaned, StringComparison.Ordinal);
    }

    [Fact]
    public void AnAnswerCutByTheTokenBudget_IsMarkedAsTruncated()
    {
        // Le verdict s'arrêtait en plein milieu de phrase quand la limite de tokens tombait.
        var cleaned = AnswerCleaner.Extract("VERDICT : 1. Mesa Boogie Dual Rectifier (1/3). C'est Drop", "VERDICT :", End);

        Assert.EndsWith(" …", cleaned, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NothingToClean_GivesNothingBack(string? text)
    {
        Assert.Equal("", AnswerCleaner.Extract(text, Start, End));
    }
}
