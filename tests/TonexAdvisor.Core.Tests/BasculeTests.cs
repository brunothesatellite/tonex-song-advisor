using System.Collections.Specialized;
using System.Globalization;
using TonexAdvisor.App.Localization;
using TonexAdvisor.App.Services;
using TonexAdvisor.App.ViewModels;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Phase 9, §11.4 : la bascule sans redémarrage se voit — bibliothèque absente, résumé,
/// listes de filtres, détail ouvert, titres d'avis, aides de réglages, libellés de voix et
/// statut se recomposent dans la langue choisie. La liste est celle du plan, §11.4.
/// </summary>
public class BasculeTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    /// <summary>Hôte sans base : les tests de réglages n'ouvrent jamais de fichier.</summary>
    private sealed class FauxHote : IDatabaseHost
    {
        public ToneXDatabase? CurrentDatabase => null;

        public Task LoadDatabaseAsync(string path) => Task.CompletedTask;
    }

    /// <summary>Dossier TONEX vide : l'analyse de la racine ne trouve rien, vite et partout.</summary>
    private static string DossierDeTest()
    {
        var folder = Path.Combine(Path.GetTempPath(), "tonex-langue-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        return folder;
    }

    [Fact]
    public void LeMessageDeBibliothequeAbsente_RecomposeSansAttache()
    {
        using var _ = new CultureGuard();
        var viewModel = new LibraryViewModel(null, new InMemoryUserStateStore());

        var francais = viewModel.EmptyMessage;
        Assert.Equal("Aucune bibliothèque chargée.", francais);

        Localizer.Instance.Culture = En;

        Assert.Equal("No library loaded.", viewModel.EmptyMessage);
    }

    [LibraryFact]
    public void LeResumeEtLesFiltres_SeReconstruisentALaBascule()
    {
        using var _ = new CultureGuard();
        var viewModel = new LibraryViewModel(null, new InMemoryUserStateStore());
        viewModel.Attach(SampleLibraries.Gen1);

        // Un vrai filtre sélectionné : c'est lui qui doit survivre à la reconstruction.
        viewModel.SelectedCategory = viewModel.Categories[1];
        viewModel.SearchText = "aucun-resultat-possible";

        var resume = viewModel.Summary;
        var categorie = viewModel.SelectedCategory;
        Assert.Contains("filtre actif", resume, StringComparison.Ordinal);

        // Les cellules de la sentinelle ne se revalident pas seules : la liste est vidée
        // puis re-remplie — c'est l'événement Reset qui prouve la reconstruction (§11.6).
        var reinitialisations = 0;
        viewModel.Categories.CollectionChanged += (_, args) =>
        {
            if (args.Action == NotifyCollectionChangedAction.Reset)
                reinitialisations++;
        };

        Localizer.Instance.Culture = En;

        Assert.NotEqual(resume, viewModel.Summary);
        Assert.Contains("active filter", viewModel.Summary, StringComparison.Ordinal);
        Assert.Equal("No result for these filters.", viewModel.EmptyMessage);
        Assert.True(reinitialisations > 0, "les listes de filtres ont été reconstruites");
        Assert.Equal(categorie, viewModel.SelectedCategory);
        Assert.Equal(LibraryViewModel.AnyFilter, viewModel.SelectedGenre);

        // Retour au français : rien ne se perd, rien ne reste figé en anglais.
        Localizer.Instance.Culture = CultureInfo.GetCultureInfo("fr-FR");
        Assert.Equal(resume, viewModel.Summary);
        Assert.Equal(categorie, viewModel.SelectedCategory);
    }

    [LibraryFact]
    public void LeDetailOvert_RenaitDansLaLangueCourante()
    {
        using var _ = new CultureGuard();
        var viewModel = new LibraryViewModel(null, new InMemoryUserStateStore());

        // Génération 2 : aucun réglage numérique — la note explicative est donc toujours
        // affichée, et son texte change de langue (§11.4).
        viewModel.Attach(SampleLibraries.Gen2);
        viewModel.SelectedPreset = viewModel.Presets[0];
        var avant = viewModel.PresetDetail;
        Assert.NotNull(avant);

        // Le texte doit être figé avant la bascule : l'expression se recompose à chaque lecture.
        var noteAvant = avant.SettingsNote;
        Assert.NotEmpty(noteAvant);

        Localizer.Instance.Culture = En;

        var apres = viewModel.PresetDetail;

        // Le détail se reconstruit : ses chaînes composées datent de sa création (§11.4).
        Assert.NotSame(avant, apres);

        var noteApres = apres!.SettingsNote;
        Assert.NotEmpty(noteApres);
        Assert.NotEqual(noteAvant, noteApres);
    }

    [Fact]
    public void LeTitreDesVoix_RecomposeAuChangementDeLangue()
    {
        using var _ = new CultureGuard();
        var library = new LibraryViewModel(null, new InMemoryUserStateStore());
        var advice = library.Advice;

        Assert.Equal("AVIS DES VOIX", advice.VoicesTitle);

        Localizer.Instance.Culture = En;

        Assert.Equal("VOICE OPINIONS", advice.VoicesTitle);
    }

    [Fact]
    public void LeChoixDeLangue_SAppliqueImmediatementEtSePersiste()
    {
        using var _ = new CultureGuard();
        var folder = DossierDeTest();

        try
        {
            var store = new InMemoryUserStateStore();
            var settings = new SettingsViewModel(new FauxHote(), store, folder);

            var depart = settings.UiLanguage;
            var autre = depart == UiLanguages.French ? UiLanguages.English : UiLanguages.French;

            settings.UiLanguage = autre;

            Assert.Equal(autre == UiLanguages.French ? "fr-FR" : "en-US", Localizer.Instance.Culture.Name);
            Assert.Equal(autre, store.Load().UiLanguage);

            // L'aller-retour écrit le dernier choix — et la culture suit à chaque fois (§10).
            settings.UiLanguage = depart;
            Assert.Equal(depart, store.Load().UiLanguage);
            Assert.Equal(depart == UiLanguages.French ? "fr-FR" : "en-US", Localizer.Instance.Culture.Name);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void LesAidesDeReglagesEtLesLibellesDeVoix_Recomposent()
    {
        using var _ = new CultureGuard();
        var folder = DossierDeTest();

        try
        {
            var settings = new SettingsViewModel(new FauxHote(), new InMemoryUserStateStore(), folder);

            var aide = settings.TonexFolderHint;
            var notice = settings.ReadOnlyNotice;
            var voix = settings.VoiceChoices;
            Assert.NotEmpty(voix);
            var premierLibelle = voix[0].Label;

            Assert.Contains("Aucune base trouvée", aide, StringComparison.Ordinal);

            Localizer.Instance.Culture = En;

            Assert.Contains("No database found in", settings.TonexFolderHint, StringComparison.Ordinal);
            Assert.NotEqual(notice, settings.ReadOnlyNotice);

            // Libellés de modèles : la ligne se re-notifie, la valeur se recalcule (§11.4).
            Assert.NotEqual(premierLibelle, voix[0].Label);
            Assert.Contains(voix[0].Model.IsFree ? "free" : "paid", voix[0].Label,
                StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    [Fact]
    public void LaSentinelleDesFiltres_AfficheLaLangueCourante()
    {
        using var _ = new CultureGuard();
        var converter = new FilterLabelConverter();

        Assert.Equal("Tous", converter.Convert("Tous", typeof(string), null, CultureInfo.InvariantCulture));

        Localizer.Instance.Culture = En;

        Assert.Equal("All", converter.Convert("Tous", typeof(string), null, CultureInfo.InvariantCulture));
    }

    [LibraryFact]
    public async Task LeStatut_RecomposeAuChangementDeLangue()
    {
        using var _ = new CultureGuard();
        var viewModel = new MainViewModel(new InMemoryUserStateStore());

        // Génération 2 : le suffixe du résumé (« réglages joints » ou « non disponibles »)
        // est entièrement localisé — le français et l'anglais ne peuvent pas coïncider. La
        // base reste ouverte : la VM est abonnée à CultureChanged pour toujours (abonnement
        // jamais désabonné, précédent phase 1) — la refermer casserait les bascules suivantes.
        await viewModel.LoadDatabaseAsync(TestPaths.Require(TestPaths.V2));

        var francais = viewModel.StatusMessage;
        Assert.NotEmpty(francais);

        Localizer.Instance.Culture = En;
        var anglais = viewModel.StatusMessage;

        Assert.NotEqual(francais, anglais);
    }
}
