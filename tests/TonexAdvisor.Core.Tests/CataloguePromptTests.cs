using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The catalogue is what the AIs choose from: names only, but the captured blocks must stay
/// paired — sending three flat lists would invite the one combination TONEX refuses.
/// </summary>
public class CataloguePromptTests
{
    [LibraryFact]
    public void Catalogue_ListsTheBlocksAndTheCabinets_NamesOnly()
    {
        var index = SampleLibraries.Gen1;
        var prompt = CataloguePrompt.Build(new AdviceQuery { Style = "metal" }, index);

        // Blocs et baffles y sont, en deux listes séparées.
        Assert.Contains("Blocs capturés disponibles", prompt, StringComparison.Ordinal);
        Assert.Contains("Baffles disponibles", prompt, StringComparison.Ordinal);

        // Noms seulement : aucune des métadonnées du classement n'y traîne.
        Assert.DoesNotContain("catégorie", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("score", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("preset «", prompt, StringComparison.OrdinalIgnoreCase);

        // La demande, elle, est rappelée.
        Assert.Contains("style « metal »", prompt, StringComparison.Ordinal);
    }

    [LibraryFact]
    public void CapturedBlocksStayPaired_StompAndAmpAreNeverSeparateLists()
    {
        var index = SampleLibraries.Gen1;

        // Une capture réelle qui porte les deux.
        var model = index.ToneModels.First(candidate =>
            candidate.AmpName.Length > 0 && candidate.StompName.Length > 0);

        var catalogue = CataloguePrompt.DescribeCatalogue(index);

        Assert.Contains(CataloguePrompt.BlockName(model), catalogue, StringComparison.Ordinal);

        // Et la règle est écrite noir sur blanc, vu que le catalogue invite à choisir un bloc.
        Assert.Contains("inséparable", catalogue + CataloguePrompt.Build(new AdviceQuery(), index), StringComparison.Ordinal);
    }

    [LibraryFact]
    public void TheAnswerFormatIsImposed_SoTheAppCanOpenWhatWasRecommended()
    {
        var prompt = CataloguePrompt.Build(new AdviceQuery { Style = "blues" }, SampleLibraries.Gen1);

        Assert.Contains("BLOC :", prompt, StringComparison.Ordinal);
        Assert.Contains("BAFFLE :", prompt, StringComparison.Ordinal);
        Assert.Contains("RÉGLAGES :", prompt, StringComparison.Ordinal);
        Assert.Contains("ALTERNATIVE :", prompt, StringComparison.Ordinal);
        Assert.Contains("N'invente aucun nom", prompt, StringComparison.Ordinal);
    }

    [LibraryFact]
    public void TheCatalogueStaysInsideAReasonableBudget()
    {
        var prompt = CataloguePrompt.Build(new AdviceQuery { Style = "metal" }, SampleLibraries.Gen1);

        // Mesuré : ~38 000 caractères pour 3 097 tone models. La marge couvre une bibliothèque
        // plus grosse sans exploser le contexte.
        Assert.True(prompt.Length < 200_000, $"the prompt grew to {prompt.Length} characters");
    }

    [LibraryFact]
    public void AQueryWithoutStyle_StillGetsTheWholeCatalogue()
    {
        var prompt = CataloguePrompt.Build(new AdviceQuery(), SampleLibraries.Gen1);

        Assert.Contains("Blocs capturés disponibles", prompt, StringComparison.Ordinal);
        Assert.Contains("Baffles disponibles", prompt, StringComparison.Ordinal);
    }
}
