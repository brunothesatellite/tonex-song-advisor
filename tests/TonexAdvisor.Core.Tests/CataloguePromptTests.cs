using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The catalogue is what the AIs choose from: names only, capped to what the free tiers accept,
/// and a stomp is always listed with its amplifier — the pairing is the rule.
/// </summary>
public class CataloguePromptTests
{
    [LibraryFact]
    public void Catalogue_ListsTheAmplifiersWithTheirStomps_AndTheCabinets()
    {
        var index = SampleLibraries.Gen1;
        var prompt = CataloguePrompt.Build(new AdviceQuery { Style = "metal" }, index);

        Assert.Contains("Blocs capturés disponibles", prompt, StringComparison.Ordinal);
        Assert.Contains("Baffles disponibles", prompt, StringComparison.Ordinal);

        // Noms seulement : aucune métadonnée du classement ne traîne.
        Assert.DoesNotContain("catégorie", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("score", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("preset «", prompt, StringComparison.OrdinalIgnoreCase);

        // La demande, elle, est rappelée.
        Assert.Contains("style « metal »", prompt, StringComparison.Ordinal);
    }

    [LibraryFact]
    public void AStompIsAlwaysListedWithItsAmplifier_NeverOnItsOwn()
    {
        var index = SampleLibraries.Gen1;

        // La capture la plus utilisée qui porte les deux : elle est forcément dans le catalogue.
        var model = index.ToneModels
            .Where(candidate => candidate.AmpName.Length > 2
                                && candidate.StompName.Length > 2
                                && !candidate.StompName.All(char.IsDigit))
            .OrderByDescending(candidate => index.PresetsFor(candidate.Key).Count)
            .First();

        var line = CataloguePrompt.DescribeCatalogue(index)
            .Split('\n')
            .First(text => text.StartsWith(model.AmpName, StringComparison.OrdinalIgnoreCase));

        Assert.Contains(model.StompName, line, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(" : ", line, StringComparison.Ordinal);
    }

    [LibraryFact]
    public void TheCatalogueIsCappedToWhatFreeTiersAccept()
    {
        var index = SampleLibraries.Gen1;
        var catalogue = CataloguePrompt.DescribeCatalogue(index);

        var amplifierLines = catalogue.Split('\n').Count(line => line.Contains(" : ", StringComparison.Ordinal));
        Assert.True(amplifierLines <= CataloguePrompt.MaxAmps, $"the catalogue carries {amplifierLines} amplifiers");

        // Mesuré : ~3 400 tokens de catalogue. Le gratuit de Groq plafonne à 8 000 tokens par
        // minute, réponse comprise — il faut donc rester loin en dessous.
        var prompt = CataloguePrompt.Build(new AdviceQuery { Style = "metal" }, index);
        Assert.True(prompt.Length < 25_000, $"the prompt grew to {prompt.Length} characters");
        Assert.Equal(3000, CataloguePrompt.MaxTokens);
    }

    [LibraryFact]
    public void NoiseIsLeftOut_SoTheListStaysReadable()
    {
        var catalogue = CataloguePrompt.DescribeCatalogue(SampleLibraries.Gen1);

        Assert.DoesNotContain("Signal Chain", catalogue, StringComparison.OrdinalIgnoreCase);
    }

    [LibraryFact]
    public void TheAnswerFormatIsImposed_SoTheAppCanOpenWhatWasRecommended()
    {
        var prompt = CataloguePrompt.Build(new AdviceQuery { Style = "blues" }, SampleLibraries.Gen1);

        Assert.Contains("BLOC :", prompt, StringComparison.Ordinal);
        Assert.Contains("BAFFLE :", prompt, StringComparison.Ordinal);
        Assert.Contains("RÉGLAGES :", prompt, StringComparison.Ordinal);
        Assert.Contains("ALTERNATIVE :", prompt, StringComparison.Ordinal);
        Assert.Contains("CONSEIL LIBRE", prompt, StringComparison.Ordinal);
        Assert.Contains("N'invente aucun nom", prompt, StringComparison.Ordinal);
    }

    [LibraryFact]
    public void TheFreeAdviceEscapesTheCatalogue_ConstraintAppliesToTheNamedChoicesOnly()
    {
        var prompt = CataloguePrompt.Build(new AdviceQuery { Style = "metal" }, SampleLibraries.Gen1);

        // La contrainte « n'invente rien » vaut pour les choix nommés, pas pour le savoir du modèle.
        Assert.Contains("CONSEIL LIBRE, lui, est ouvert à tout ton savoir", prompt, StringComparison.Ordinal);
    }

    [LibraryFact]
    public void AQueryWithoutStyle_StillGetsTheCatalogue()
    {
        var prompt = CataloguePrompt.Build(new AdviceQuery(), SampleLibraries.Gen1);

        Assert.Contains("Blocs capturés disponibles", prompt, StringComparison.Ordinal);
        Assert.Contains("Baffles disponibles", prompt, StringComparison.Ordinal);
    }
}
