using TonexAdvisor.App.Services;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Les avis arrivent dans l'ordre du réseau, qui change à chaque exécution : le panneau, lui, doit
/// toujours se lire de la même façon — référence, voix, puis avis convergé.
/// </summary>
public class OpinionOrderTests
{
    [Fact]
    public void TheReferenceComesFirst_ThenTheChallengersInCatalogueOrder()
    {
        Assert.True(OpinionOrder.Rank("OpenCode Go") < OpinionOrder.Rank("Gemini (Google)"));
        Assert.True(OpinionOrder.Rank("Gemini (Google)") < OpinionOrder.Rank("Mistral"));
        Assert.True(OpinionOrder.Rank("Mistral") < OpinionOrder.Rank("Groq"));
        Assert.True(OpinionOrder.Rank("Groq") < OpinionOrder.Rank("Une voix inconnue"));
    }

    [Fact]
    public void ThePanelReadsTheSameWayWhateverTheAnswerOrder()
    {
        var providers = new[] { "Groq", "Mistral", "OpenCode Go", "Gemini (Google)" };
        var sorted = providers.OrderBy(OpinionOrder.Rank).ToArray();

        Assert.Equal(new[] { "OpenCode Go", "Gemini (Google)", "Mistral", "Groq" }, sorted);
    }
}
