using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The prompt is the only thing the AI ever sees: it must stay short, stay on the shortlist and
/// repeat the rule a model would otherwise bend.
/// </summary>
public class AdvicePromptTests
{
    [Fact]
    public void Prompt_ContainsTheShortlistOnly_WithOneLinePerPreset()
    {
        var index = SampleLibraries.Gen1;
        var advisor = new LibraryAdvisor(index);
        var query = new AdviceQuery { Style = "metal" };
        var presets = advisor.RankPresets(query, 3);
        var combinations = advisor.RankCombinations(query, 1);

        Assert.Equal(3, presets.Count);
        Assert.NotEmpty(combinations);

        var prompt = AdvicePrompt.Build(query, presets, combinations[0], index);

        foreach (var scored in presets)
            Assert.Contains(scored.Preset.Name, prompt, StringComparison.Ordinal);

        // Exactly one « preset « … » » line per shortlisted preset: no stray rows of the library.
        Assert.Equal(presets.Count, CountOccurrences(prompt, "preset «"));
        Assert.True(prompt.Length < 4000, $"the prompt grew to {prompt.Length} characters");
    }

    [Fact]
    public void Prompt_StatesTheCapturedBlockRule()
    {
        var index = SampleLibraries.Gen1;
        var advisor = new LibraryAdvisor(index);
        var query = new AdviceQuery { Style = "metal" };
        var presets = advisor.RankPresets(query, 3);
        var combination = advisor.RankCombinations(query, 1)[0];

        var prompt = AdvicePrompt.Build(query, presets, combination, index);

        Assert.Contains("inséparables", prompt, StringComparison.Ordinal);
        Assert.Contains("seul le baffle se change", prompt, StringComparison.Ordinal);
        Assert.Contains("N'invente aucun preset", prompt, StringComparison.Ordinal);

        // The recommendation itself travels with the prompt.
        Assert.Contains(combination.Amp, prompt, StringComparison.Ordinal);
        Assert.Contains("baffle suggéré", prompt, StringComparison.Ordinal);

        // The request is quoted so the model cannot answer about another song.
        Assert.Contains("style « metal »", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_QuotesTheKnobValues_WhenTheFormatCarriesThem()
    {
        var index = SampleLibraries.Gen1;
        var preset = index.Presets.First(candidate => candidate.HasKnobSettings);
        var scored = new ScoredPreset(preset, 100, [new ScoreReason("test", 1)]);

        var prompt = AdvicePrompt.Build(
            new AdviceQuery { Style = "metal" }, [scored], null, index);

        Assert.Contains("ModelGain=", prompt, StringComparison.Ordinal);
        Assert.Contains("EqBass=", prompt, StringComparison.Ordinal);
    }

    [Fact]
    public void Prompt_NeverInventsSettingsForAGeneration2Library()
    {
        var index = SampleLibraries.Gen2;
        var advisor = new LibraryAdvisor(index);
        var presets = advisor.RankPresets(new AdviceQuery { Style = "clean" }, 3);

        Assert.NotEmpty(presets);

        var prompt = AdvicePrompt.Build(new AdviceQuery { Style = "clean" }, presets, null, index);

        Assert.DoesNotContain("ModelGain=", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("ModelVolume=", prompt, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string fragment)
    {
        var count = 0;
        for (var index = text.IndexOf(fragment, StringComparison.Ordinal);
             index >= 0;
             index = text.IndexOf(fragment, index + fragment.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
