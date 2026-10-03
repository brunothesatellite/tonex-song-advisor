using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The referee hears everyone and believes no one: the prompt must carry every voice, the
/// instruction to challenge them, and the catalogue they all have to choose from.
/// </summary>
public class ArbitrationPromptTests
{
    [LibraryFact]
    public void Referee_HearsEveryVoice_AndIsToldToChallengeThem()
    {
        var index = SampleLibraries.Gen1;
        var query = new AdviceQuery { Style = "metal" };

        var opinions = new[]
        {
            new Opinion("Gemini (Google)", "BLOC : Ibanez Tube Screamer TS808 -> Mesa Boogie Triple Rectifier"),
            new Opinion("Mistral", "Non : ce stomp vient d'une autre capture que cet ampli."),
            new Opinion("Groq", "Je préfère un Marshall JCM 800."),
        };

        var prompt = ArbitrationPrompt.Build(query, opinions, index);

        // Every voice is quoted, with what it said.
        foreach (var opinion in opinions)
        {
            Assert.Contains(opinion.Provider, prompt, StringComparison.Ordinal);
            Assert.Contains(opinion.Text, prompt, StringComparison.Ordinal);
        }

        // The catalogue is there: the opinions can only be judged against it.
        Assert.Contains("Blocs capturés disponibles", prompt, StringComparison.Ordinal);
        Assert.Contains("Baffles disponibles", prompt, StringComparison.Ordinal);

        // The mission: confront, rank three proposals, stay inside the catalogue.
        Assert.Contains("Confronte les avis", prompt, StringComparison.Ordinal);
        Assert.Contains("3 meilleures propositions", prompt, StringComparison.Ordinal);
        Assert.Contains("consensus", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("N'invente aucun bloc", prompt, StringComparison.Ordinal);

        // And the captured block rule is recalled to the referee itself.
        Assert.Contains("inséparable", prompt, StringComparison.Ordinal);
    }

    [LibraryFact]
    public void Referee_IsToldThatUnanimityIsNotProof()
    {
        var index = SampleLibraries.Gen1;

        var prompt = ArbitrationPrompt.Build(
            new AdviceQuery { Style = "blues" },
            [new Opinion("Gemini (Google)", "Tout le monde a raison, prenez le même bloc.")],
            index);

        Assert.Contains("unanime n'est pas une preuve", prompt, StringComparison.Ordinal);
    }

    [LibraryFact]
    public void Referee_ToleratesAVoiceThatSaidNothing()
    {
        var index = SampleLibraries.Gen1;

        var prompt = ArbitrationPrompt.Build(
            new AdviceQuery { Style = "clean" },
            [new Opinion("Mistral", ""), new Opinion("Groq", "Le bloc Fender 65 Deluxe Reverb.")],
            index);

        Assert.Contains("Le bloc Fender 65 Deluxe Reverb.", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("--- Mistral ---", prompt, StringComparison.Ordinal);
    }
}
