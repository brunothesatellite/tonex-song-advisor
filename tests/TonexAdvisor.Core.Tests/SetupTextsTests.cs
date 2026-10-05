using System.Globalization;
using System.Text.RegularExpressions;
using TonexAdvisor.Setup;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// G10 (§13), phase 11 : l'installeur bilingue. Table FR/EN en code (décision 2), règle de
/// langue identique à celle de l'application (§11), aucune chaîne orpheline.
/// </summary>
public class SetupTextsTests
{
    /// <summary>Indices des emplacements d'une valeur (<c>{0:N0}</c> → 0), format ignoré.</summary>
    private static HashSet<int> Emplacements(string valeur)
        => Regex.Matches(valeur, @"\{(\d+)(?::[^{}]*)?\}")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToHashSet();

    [Fact]
    public void Chaque_message_de_l_installateur_a_ses_deux_langues()
    {
        // ~12 messages annoncés au §12 : la table ne doit pas se vider sous couvert de refacto.
        Assert.True(SetupTexts.Table.Count >= 12, $"table réduite à {SetupTexts.Table.Count} messages");

        foreach (var (cle, message) in SetupTexts.Table)
        {
            Assert.False(string.IsNullOrWhiteSpace(message.Fr), $"FR manquant pour '{cle}'");
            Assert.False(string.IsNullOrWhiteSpace(message.En), $"EN manquant pour '{cle}'");
            Assert.True(Emplacements(message.Fr).SetEquals(Emplacements(message.En)),
                $"les emplacements de '{cle}' divergent : FR [{string.Join(",", Emplacements(message.Fr))}] " +
                $"vs EN [{string.Join(",", Emplacements(message.En))}]");
        }
    }

    [Fact]
    public void La_langue_suit_la_culture_OS_comme_l_application()
    {
        // Même règle que §11 : « fr » → français, tout le reste → anglais (repli, décision 3).
        Assert.True(SetupTexts.UseFrench(CultureInfo.GetCultureInfo("fr-FR")));
        Assert.True(SetupTexts.UseFrench(CultureInfo.GetCultureInfo("fr-CA")));
        Assert.False(SetupTexts.UseFrench(CultureInfo.GetCultureInfo("en-US")));
        Assert.False(SetupTexts.UseFrench(CultureInfo.GetCultureInfo("de-DE")));
        Assert.False(SetupTexts.UseFrench(CultureInfo.GetCultureInfo("es-ES")));
    }

    [Fact]
    public void Un_message_s_affiche_dans_la_langue_de_l_OS_avec_ses_arguments()
    {
        Assert.Equal("Installation terminée.",
            SetupTexts.Get("installation.terminee", CultureInfo.GetCultureInfo("fr-FR")));
        Assert.Equal("Installation complete.",
            SetupTexts.Get("installation.terminee", CultureInfo.GetCultureInfo("en-US")));

        // Une machine non francophone reçoit l'anglais — jamais une console muette.
        Assert.StartsWith("Error", SetupTexts.Get("echec", CultureInfo.GetCultureInfo("de-DE"), "boom"));
        Assert.Equal("Échec : boom",
            SetupTexts.Get("echec", CultureInfo.GetCultureInfo("fr-FR"), "boom"));

        // La description des raccourcis, posée à l'installation, existe des deux côtés.
        Assert.Contains("TONEX",
            SetupTexts.Get("raccourci.description", CultureInfo.GetCultureInfo("fr-FR")));
        Assert.Contains("TONEX",
            SetupTexts.Get("raccourci.description", CultureInfo.GetCultureInfo("en-US")));
    }
}
