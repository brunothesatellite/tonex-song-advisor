using System.Globalization;
using TonexAdvisor.App.ViewModels;
using TonexAdvisor.Core.Advice;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The protocol speaks two languages without ever breaking: extraction and colouring tolerate
/// both marker sets whatever the session says, the invite alone follows the session's language.
/// </summary>
public class AnswerFormatTests
{
    private static readonly CultureInfo Francais = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly CultureInfo Anglais = CultureInfo.GetCultureInfo("en-US");

    private const string ReponseFrancaise = """
        réflexion d'avant
        BLOC : TS9 -> JCM800
        BAFFLE : Mesa 4x12
        RÉGLAGES : Gain 7
        ALTERNATIVE : AFD100 -> Jumpin Cat

        CONSEIL LIBRE : ce que tu veux.

        travail d'après
        """;

    private const string ReponseAnglaise = """
        thinking up front
        BLOCK : TS9 -> JCM800
        CAB : Mesa 4x12
        SETTINGS : Gain 7
        ALTERNATIVE : AFD100 -> Jumpin Cat

        FREE ADVICE : whatever you like.

        working after
        """;

    [Fact]
    public void Session_fr_recherche_ses_marqueurs_puis_le_repli_en()
    {
        var format = AnswerFormat.ForCulture(Francais);

        Assert.Equal(new[] { "BLOC :", "BLOCK :" }, format.VoiceStarts);
        Assert.Equal(new[] { "CONSEIL LIBRE", "FREE ADVICE" }, format.EndMarkers);
        Assert.Equal("BLOC", format.Bloc);
        Assert.Equal("BAFFLE", format.Baffle);
    }

    [Fact]
    public void Session_en_recherche_ses_marqueurs_puis_le_repli_fr()
    {
        var format = AnswerFormat.ForCulture(Anglais);

        Assert.Equal(new[] { "BLOCK :", "BLOC :" }, format.VoiceStarts);
        Assert.Equal(new[] { "FREE ADVICE", "CONSEIL LIBRE" }, format.EndMarkers);
        Assert.Equal("BLOCK", format.Bloc);
        Assert.Equal("CAB", format.Baffle);
        Assert.Equal("SETTINGS", format.Reglages);
        Assert.Equal("FREE ADVICE", format.ConseilLibre);
    }

    [Fact]
    public void Sans_culture_le_francais_reste_la_regle()
    {
        Assert.False(AnswerFormat.IsEnglish(null));
        Assert.False(AnswerFormat.IsEnglish(Francais));
        Assert.True(AnswerFormat.IsEnglish(Anglais));
        Assert.Same(AnswerFormat.French, AnswerFormat.ForCulture(null));
    }

    [Fact]
    public void Extrait_la_reponse_francaise_en_session_fr()
    {
        var result = AnswerFormat.ForCulture(Francais).ExtractVoice(ReponseFrancaise);

        Assert.StartsWith("BLOC : TS9", result, StringComparison.Ordinal);
        Assert.Contains("CONSEIL LIBRE", result, StringComparison.Ordinal);
        Assert.DoesNotContain("réflexion", result, StringComparison.Ordinal);
        Assert.DoesNotContain("travail", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extrait_la_reponse_anglaise_en_session_en()
    {
        var result = AnswerFormat.ForCulture(Anglais).ExtractVoice(ReponseAnglaise);

        Assert.StartsWith("BLOCK : TS9", result, StringComparison.Ordinal);
        Assert.Contains("FREE ADVICE", result, StringComparison.Ordinal);
        Assert.DoesNotContain("thinking", result, StringComparison.Ordinal);
        Assert.DoesNotContain("working after", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extrait_une_reponse_anglaise_en_session_fr()
    {
        // Le modèle répond en anglais par accident : l'extraction ne casse pas (§9).
        var result = AnswerFormat.ForCulture(Francais).ExtractVoice(ReponseAnglaise);

        Assert.StartsWith("BLOCK : TS9", result, StringComparison.Ordinal);
        Assert.Contains("FREE ADVICE", result, StringComparison.Ordinal);
        Assert.DoesNotContain("thinking", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extrait_une_reponse_francaise_en_session_en()
    {
        var result = AnswerFormat.ForCulture(Anglais).ExtractVoice(ReponseFrancaise);

        Assert.StartsWith("BLOC : TS9", result, StringComparison.Ordinal);
        Assert.Contains("CONSEIL LIBRE", result, StringComparison.Ordinal);
        Assert.DoesNotContain("réflexion", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Extrait_le_verdict_quel_que_soit_la_session()
    {
        var verdict = """
            draft
            VERDICT : 1. TS9 -> JCM800 (3/3)

            FREE ADVICE : take it.
            """;

        var result = AnswerFormat.English.ExtractVerdict(verdict);

        Assert.StartsWith("VERDICT :", result, StringComparison.Ordinal);
        Assert.Contains("FREE ADVICE : take it.", result, StringComparison.Ordinal);
        Assert.DoesNotContain("draft", result, StringComparison.Ordinal);
    }

    [Fact]
    public void Les_marqueurs_de_coloration_couvrent_les_deux_langues()
    {
        foreach (var label in new[]
        {
            "BLOC :", "BLOCK :", "BAFFLE :", "CAB :", "RÉGLAGES :", "SETTINGS :",
            "ALTERNATIVE :", "CONSEIL LIBRE :", "FREE ADVICE :", "VERDICT :",
        })
        {
            Assert.Contains(label, AnswerFormat.AllMarkers);
        }
    }

    [Theory]
    [InlineData("BLOC : TS9 -> JCM800", "BLOC :")]
    [InlineData("BLOCK : TS9 -> JCM800", "BLOCK :")]
    [InlineData("BAFFLE : Mesa 4x12", "BAFFLE :")]
    [InlineData("CAB : Mesa 4x12", "CAB :")]
    [InlineData("RÉGLAGES : Gain 7", "RÉGLAGES :")]
    [InlineData("SETTINGS : Gain 7", "SETTINGS :")]
    [InlineData("CONSEIL LIBRE : ouvert", "CONSEIL LIBRE :")]
    [InlineData("FREE ADVICE : open", "FREE ADVICE :")]
    public void La_coloration_peint_les_marqueurs_des_deux_langues(string line, string label)
    {
        var result = AnswerLineViewModel.Split(line).Single();

        Assert.Equal(label, result.Label);
    }
}
