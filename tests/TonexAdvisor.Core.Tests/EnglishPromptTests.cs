using System.Globalization;
using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// An English session sends English invites: same catalogue, same rules, §9 markers — and no
/// French left in what the model reads, from the language order down to the free advice.
/// </summary>
public class EnglishPromptTests
{
    private static readonly CultureInfo Anglais = CultureInfo.GetCultureInfo("en-US");

    [LibraryFact]
    public void Catalogue_en_demande_le_format_en()
    {
        var prompt = CataloguePrompt.Build(new AdviceQuery { Style = "metal" }, SampleLibraries.Gen1, Anglais);

        Assert.Contains("Answer in English", prompt, StringComparison.Ordinal);
        Assert.Contains("Captured blocks", prompt, StringComparison.Ordinal);
        Assert.Contains("The request:", prompt, StringComparison.Ordinal);
        Assert.Contains("BLOCK : <stomp>", prompt, StringComparison.Ordinal);
        Assert.Contains("CAB : <name of the chosen cabinet>", prompt, StringComparison.Ordinal);
        Assert.Contains("SETTINGS : 2 to 3 concrete settings", prompt, StringComparison.Ordinal);
        Assert.Contains("Rules:", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Réponds en français", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Blocs capturés", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Règles :", prompt, StringComparison.Ordinal);
    }

    [LibraryFact]
    public void Arbitrage_en_demande_son_verdict_en()
    {
        var opinions = new[]
        {
            new Opinion("Gemini (Google)", "BLOCK : Ibanez TS808 -> Mesa Boogie Triple Rectifier"),
        };

        var prompt = ArbitrationPrompt.Build(
            new AdviceQuery { Style = "metal" }, opinions, SampleLibraries.Gen1, Anglais);

        Assert.Contains("The opinions received:", prompt, StringComparison.Ordinal);
        Assert.Contains("Your mission, in this exact format:", prompt, StringComparison.Ordinal);
        Assert.Contains("Start at \"VERDICT :\"", prompt, StringComparison.Ordinal);
        Assert.Contains("FREE ADVICE", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Ta mission", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Commence par", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("Les avis reçus", prompt, StringComparison.Ordinal);
    }
}
