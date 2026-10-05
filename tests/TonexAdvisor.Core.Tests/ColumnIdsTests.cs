using System.Globalization;
using TonexAdvisor.App.Localization;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Phase 9, §8.2 : une largeur de colonne se souvient d'un id, jamais d'un en-tête. Un
/// fichier v1.2.1 écrit en français ou en anglais reste applicable, et la langue choisie
/// après coup ne perd aucune largeur.
/// </summary>
public class ColumnIdsTests
{
    [Fact]
    public void LesEntetesFrancaisDeLaV121_DeviennentDesIds()
    {
        var historique = new Dictionary<string, double>
        {
            ["Nom"] = 200,
            ["Catégorie"] = 132,
            ["Réglages"] = 88,
        };

        var normalisees = ColumnIds.Normalize(ColumnIds.Grid.Presets, historique);

        Assert.Equal(200, normalisees["name"]);
        Assert.Equal(132, normalisees["category"]);
        Assert.Equal(88, normalisees["settings"]);
        Assert.False(normalisees.ContainsKey("Nom"));
    }

    [Fact]
    public void LesEntetesAnglais_SeConvertissentAussi()
    {
        var historique = new Dictionary<string, double>
        {
            ["Name"] = 180,
            ["Cabinet"] = 150,
            ["Mics"] = 104,
        };

        var normalisees = ColumnIds.Normalize(ColumnIds.Grid.ToneModels, historique);

        Assert.Equal(180, normalisees["name"]);
        Assert.Equal(150, normalisees["cab"]);
        Assert.Equal(104, normalisees["mics"]);
    }

    [Fact]
    public void UnIdPasseTelQuel_UneCleInconnueEstConservee()
    {
        var largeurs = new Dictionary<string, double>
        {
            ["name"] = 200,
            ["Futur"] = 90,
        };

        var normalisees = ColumnIds.Normalize(ColumnIds.Grid.Presets, largeurs);

        Assert.Equal(200, normalisees["name"]);
        Assert.Equal(90, normalisees["Futur"]);
        Assert.Null(ColumnIds.IdOf(ColumnIds.Grid.Presets, "Futur"));
    }

    [Fact]
    public void LEnteteCourante_SuitLaLangueAffichee_MemePourUneCleHistorique()
    {
        using var _ = new CultureGuard();

        // Français : l'id et la clé historique donnent le même en-tête.
        Assert.Equal("Nom", ColumnIds.HeaderOf(ColumnIds.Grid.Presets, "name"));
        Assert.Equal("Nom", ColumnIds.HeaderOf(ColumnIds.Grid.Presets, "Nom"));

        Localizer.Instance.Culture = CultureInfo.GetCultureInfo("en-US");

        // Anglais : la clé historique française se route par l'id — même largeur, autre langue.
        Assert.Equal("Name", ColumnIds.HeaderOf(ColumnIds.Grid.Presets, "name"));
        Assert.Equal("Name", ColumnIds.HeaderOf(ColumnIds.Grid.Presets, "Nom"));
    }
}
