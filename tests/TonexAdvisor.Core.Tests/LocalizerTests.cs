using System.ComponentModel;
using System.Globalization;
using TonexAdvisor.App.Localization;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Tests du socle de localisation (phase 8, §5 à §6) : indexeur, bascule de culture,
/// formatage calé sur la culture, pluriels CLDR. Les valeurs FR citées en dur sont
/// extraites verbatim des ressources — c'est aussi le premier échantillon doré (G4).
/// </summary>
[Collection(LocalisationCollection.Nom)]
public class LocalizerTests
{
    [Fact]
    public void Indexeur_lit_le_franc_ais_par_defaut()
    {
        using var _ = new CultureGuard();

        Assert.Equal("Nom", Localizer.Instance["Biblio.Col.Nom"]);
        Assert.Equal("RECHERCHE", Localizer.Instance["Biblio.Recherche.Titre"]);
        Assert.Equal("BLOC :", Localizer.Instance["Marqueur.Bloc"]);
    }

    [Fact]
    public void Bascule_anglais_recharge_l_indexeur_et_notifie()
    {
        using var _ = new CultureGuard();
        var notifications = new List<string?>();
        PropertyChangedEventHandler handler = (_, e) => notifications.Add(e.PropertyName);
        var changementsDeCulture = 0;
        Localizer.Instance.PropertyChanged += handler;
        Localizer.Instance.CultureChanged += () => changementsDeCulture++;

        try
        {
            Assert.Equal("Nom", Localizer.Instance["Biblio.Col.Nom"]);

            Localizer.Instance.Culture = CultureInfo.GetCultureInfo("en-US");

            Assert.Equal("Name", Localizer.Instance["Biblio.Col.Nom"]);
            Assert.Equal("SEARCH", Localizer.Instance["Biblio.Recherche.Titre"]);
            Assert.Contains("Item[]", notifications);
            Assert.Equal(1, changementsDeCulture);
        }
        finally
        {
            Localizer.Instance.PropertyChanged -= handler;
        }
    }

    [Fact]
    public void Meme_culture_reposee_leve_aucun_evenement()
    {
        using var _ = new CultureGuard();
        var changements = 0;
        Localizer.Instance.CultureChanged += () => changements++;

        Localizer.Instance.Culture = CultureInfo.GetCultureInfo("fr-FR");

        Assert.Equal(0, changements);
    }

    [Fact]
    public void Formatage_des_nombres_suit_la_culture()
    {
        using var _ = new CultureGuard();
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        var en = CultureInfo.GetCultureInfo("en-US");

        static string Resume() => Localizer.Instance.Get(
            "Message.Resume.Bibliotheque",
            Localizer.Instance.Plural(2310, "Compteur.Presets"),
            Localizer.Instance.Plural(15, "Compteur.ToneModels"));

        var message = Resume();
        Assert.Equal(
            $"{2310.ToString("N0", fr)} presets · {15.ToString("N0", fr)} tone models",
            message);

        Localizer.Instance.Culture = en;
        message = Resume();
        Assert.Equal(
            $"{2310.ToString("N0", en)} presets · {15.ToString("N0", en)} tone models",
            message);
    }

    [Fact]
    public void Mise_a_plat_simple_substitue_les_emplacements()
    {
        using var _ = new CultureGuard();

        var message = Localizer.Instance.Get("Message.Echec", "détail de l'erreur");

        Assert.Equal("Échec : détail de l'erreur", message);
    }

    [Fact]
    public void Pluriel_fr_met_zero_et_un_au_singulier()
    {
        using var _ = new CultureGuard();
        const string baseKey = "Conseil.Combination.Uses";

        Assert.Equal("Utilisé par 0 preset de la bibliothèque", Localizer.Instance.Plural(0, baseKey));
        Assert.Equal("Utilisé par 1 preset de la bibliothèque", Localizer.Instance.Plural(1, baseKey));
        Assert.Equal("Utilisé par 2 presets de la bibliothèque", Localizer.Instance.Plural(2, baseKey));
        Assert.Equal("Utilisé par 2310 presets de la bibliothèque", Localizer.Instance.Plural(2310, baseKey));
    }

    [Fact]
    public void Pluriel_anglais_met_un_seul_au_singulier()
    {
        using var _ = new CultureGuard();
        Localizer.Instance.Culture = CultureInfo.GetCultureInfo("en-US");
        const string baseKey = "Conseil.Combination.Uses";

        Assert.Equal("Used by 0 presets in the library", Localizer.Instance.Plural(0, baseKey));
        Assert.Equal("Used by 1 preset in the library", Localizer.Instance.Plural(1, baseKey));
        Assert.Equal("Used by 2 presets in the library", Localizer.Instance.Plural(2, baseKey));
    }
}
