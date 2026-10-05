using System.Globalization;
using TonexAdvisor.App.Localization;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Tests des pluriels et formats de la phase 8 (§6) : paires de compteurs à 0/1/2
/// selon la règle de chaque langue, clé à argument supplémentaire, et libellé
/// culture-dépendant. Les valeurs citées sont extraites verbatim des ressources (G4).
/// </summary>
[Collection(LocalisationCollection.Nom)]
public class PluralisationTests
{
    [Fact]
    public void Compteurs_fr_met_0_et_1_au_singulier()
    {
        using var _ = new CultureGuard();

        Assert.Equal("0 preset", Localizer.Instance.Plural(0, "Compteur.Presets"));
        Assert.Equal("1 preset", Localizer.Instance.Plural(1, "Compteur.Presets"));
        Assert.Equal("2 presets", Localizer.Instance.Plural(2, "Compteur.Presets"));
        Assert.Equal("0 tone model", Localizer.Instance.Plural(0, "Compteur.ToneModels"));
        Assert.Equal("1 tone model", Localizer.Instance.Plural(1, "Compteur.ToneModels"));
        Assert.Equal("2 tone models", Localizer.Instance.Plural(2, "Compteur.ToneModels"));
    }

    [Fact]
    public void Compteurs_en_met_seul_1_au_singulier()
    {
        using var _ = new CultureGuard();
        Localizer.Instance.Culture = CultureInfo.GetCultureInfo("en-US");

        Assert.Equal("0 presets", Localizer.Instance.Plural(0, "Compteur.Presets"));
        Assert.Equal("1 preset", Localizer.Instance.Plural(1, "Compteur.Presets"));
        Assert.Equal("2 presets", Localizer.Instance.Plural(2, "Compteur.Presets"));
    }

    [Fact]
    public void Pluriel_accepte_un_argument_complementaire()
    {
        using var _ = new CultureGuard();

        var singulier = Localizer.Instance.Plural(1, "Reglages.Bases.Trouvees", @"C:\Tonex");
        var pluriel = Localizer.Instance.Plural(3, "Reglages.Bases.Trouvees", @"C:\Tonex");

        Assert.Contains("1 base trouvée dans C:\\Tonex", singulier, StringComparison.Ordinal);
        Assert.Contains("3 bases trouvées dans C:\\Tonex", pluriel, StringComparison.Ordinal);
    }

    [Fact]
    public void Pourcent_epargne_l_espace_selon_la_langue()
    {
        using var _ = new CultureGuard();

        Assert.Equal("87 %", Localizer.Instance.Get("Conseil.Score.Pourcent", 87));

        Localizer.Instance.Culture = CultureInfo.GetCultureInfo("en-US");
        Assert.Equal("87%", Localizer.Instance.Get("Conseil.Score.Pourcent", 87));
    }
}
