using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The referee hears everyone and believes no one: the prompt must carry every voice, the
/// instruction to challenge them, and the constraint they all have to respect.
/// </summary>
public class ArbitrationPromptTests
{
    [LibraryFact]
    public void Referee_HearsEveryVoice_AndIsToldToChallengeThem()
    {
        var index = SampleLibraries.Gen1;
        var advisor = new LibraryAdvisor(index);
        var query = new AdviceQuery { Style = "metal" };
        var presets = advisor.RankPresets(query, 3);
        var combination = advisor.RankCombinations(query, 1)[0];

        var opinions = new[]
        {
            new Opinion("Gemini (Google)", "Prends le preset 1, le bloc est idéal."),
            new Opinion("Mistral", "Non : le preset 1 associe un stomp à un ampli d'une autre capture."),
            new Opinion("Groq", "Je préfère le preset 3."),
        };

        var prompt = ArbitrationPrompt.Build(query, presets, combination, opinions, index);

        // Every voice is quoted, with what it said.
        foreach (var opinion in opinions)
        {
            Assert.Contains(opinion.Provider, prompt, StringComparison.Ordinal);
            Assert.Contains(opinion.Text, prompt, StringComparison.Ordinal);
        }

        // The mission: confront, rank three proposals, stay inside the selection.
        Assert.Contains("Confronte les avis", prompt, StringComparison.Ordinal);
        Assert.Contains("3 meilleures propositions", prompt, StringComparison.Ordinal);
        Assert.Contains("consensus", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("N'invente aucun preset", prompt, StringComparison.Ordinal);

        // The captured block rule is recalled to the referee itself.
        Assert.Contains("inséparables", prompt, StringComparison.Ordinal);

        // The shortlist is there: the opinions can only be judged against it.
        foreach (var scored in presets)
            Assert.Contains(scored.Preset.Name, prompt, StringComparison.Ordinal);
    }

    [LibraryFact]
    public void Referee_IsToldThatUnanimityIsNotProof()
    {
        var index = SampleLibraries.Gen1;
        var advisor = new LibraryAdvisor(index);
        var query = new AdviceQuery { Style = "blues" };
        var presets = advisor.RankPresets(query, 3);

        var prompt = ArbitrationPrompt.Build(
            query,
            presets,
            null,
            [new Opinion("Gemini (Google)", "Tout le monde a raison, prenez le 1.")],
            index);

        Assert.Contains("unanime n'est pas une preuve", prompt, StringComparison.Ordinal);
    }

    [LibraryFact]
    public void Referee_ToleratesAVoiceThatSaidNothing()
    {
        var index = SampleLibraries.Gen1;
        var advisor = new LibraryAdvisor(index);
        var query = new AdviceQuery { Style = "clean" };
        var presets = advisor.RankPresets(query, 3);

        var prompt = ArbitrationPrompt.Build(
            query,
            presets,
            null,
            [new Opinion("Mistral", ""), new Opinion("Groq", "Le preset 2.")],
            index);

        Assert.Contains("Le preset 2.", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("--- Mistral ---", prompt, StringComparison.Ordinal);
    }
}
