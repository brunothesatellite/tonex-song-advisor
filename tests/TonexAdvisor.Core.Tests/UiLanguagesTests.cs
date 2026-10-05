using System.Globalization;
using TonexAdvisor.App.Localization;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Phase 9, §11 : le choix mémorisé d'abord, la machine ensuite — « fr » renvoie au
/// français, tout le reste (en, en-US, de, es, pt…) bascule en anglais (repli anglais).
/// </summary>
public class UiLanguagesTests
{
    [Fact]
    public void UnChoixMemorise_GagneSurLaDetection()
    {
        Assert.Equal("fr", UiLanguages.Resolve("fr", CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("en", UiLanguages.Resolve("en", CultureInfo.GetCultureInfo("fr-FR")));
    }

    [Fact]
    public void PremierLancement_LeFrancaisSeulDeclencheLeFrancais()
    {
        Assert.Equal("fr", UiLanguages.Resolve(null, CultureInfo.GetCultureInfo("fr-FR")));
        Assert.Equal("fr", UiLanguages.Resolve(null, CultureInfo.GetCultureInfo("fr-CA")));
    }

    [Fact]
    public void PremierLancement_ToutLeResteVaEnAnglais()
    {
        Assert.Equal("en", UiLanguages.Resolve(null, CultureInfo.GetCultureInfo("en-US")));
        Assert.Equal("en", UiLanguages.Resolve(null, CultureInfo.GetCultureInfo("en-GB")));
        Assert.Equal("en", UiLanguages.Resolve(null, CultureInfo.GetCultureInfo("de-DE")));
        Assert.Equal("en", UiLanguages.Resolve(null, CultureInfo.GetCultureInfo("es-ES")));
        Assert.Equal("en", UiLanguages.Resolve(null, CultureInfo.GetCultureInfo("pt-BR")));
    }

    [Fact]
    public void UneLangueHorsContrat_SeRedetecte()
    {
        Assert.Equal("fr", UiLanguages.Resolve("de", CultureInfo.GetCultureInfo("fr-FR")));
        Assert.Equal("en", UiLanguages.Resolve("pt", CultureInfo.GetCultureInfo("en-US")));
    }

    [Fact]
    public void LesCultures_SontCellesDesQuatreRegleurs()
    {
        Assert.Equal("fr-FR", UiLanguages.CultureOf(UiLanguages.French).Name);
        Assert.Equal("en-US", UiLanguages.CultureOf(UiLanguages.English).Name);
    }

    [Fact]
    public void LesDeuxLangues_SontProposeesDansLOrdre()
    {
        Assert.Equal(new[] { "fr", "en" }, UiLanguages.Codes);
        Assert.True(UiLanguages.IsSupported("fr"));
        Assert.True(UiLanguages.IsSupported("en"));
        Assert.False(UiLanguages.IsSupported("de"));
        Assert.False(UiLanguages.IsSupported(null));
    }
}
