using TonexAdvisor.App.ViewModels;

namespace TonexAdvisor.Core.Tests;

public class AnswerLineViewModelTests
{
    [Fact]
    public void Split_colore_un_libelle_en_debut_de_ligne()
    {
        var line = AnswerLineViewModel.Split("BAFFLE : Rectifier 2x12").Single();

        Assert.Equal("BAFFLE :", line.Label);
        Assert.Equal("Rectifier 2x12", line.Body);
    }

    [Theory]
    [InlineData("   BAFFLE : Rectifier 2x12")]
    [InlineData("\tBAFFLE : Rectifier 2x12")]
    [InlineData(" - BAFFLE : Rectifier 2x12")]
    [InlineData("* BAFFLE : Rectifier 2x12")]
    [InlineData("**BAFFLE** : Rectifier 2x12")]
    public void Split_colore_un_libelle_masque_par_un_retrait_ou_une_puce(string line)
    {
        var result = AnswerLineViewModel.Split(line).Single();

        Assert.Equal("BAFFLE :", result.Label);
        Assert.Equal("Rectifier 2x12", result.Body);
    }

    [Fact]
    public void Split_colore_un_libelle_separe_par_une_espace_insecable()
    {
        var line = AnswerLineViewModel.Split("BLOC\u00A0: Plexi").Single();

        Assert.Equal("BLOC :", line.Label);
        Assert.Equal("Plexi", line.Body);
    }

    [Theory]
    [InlineData("RÉGLAGES: gain 6")]
    [InlineData("RÉGLAGES : gain 6")]
    [InlineData("RÉGLAGES  : gain 6")]
    public void Split_tolere_l_espace_avant_le_deux_points(string line)
    {
        var result = AnswerLineViewModel.Split(line).Single();

        Assert.Equal("RÉGLAGES :", result.Label);
        Assert.Equal("gain 6", result.Body);
    }

    [Fact]
    public void Split_colore_le_numero_d_une_proposition()
    {
        var line = AnswerLineViewModel.Split("  1. Mesa Boogie Dual Rectifier").Single();

        Assert.Equal("1.", line.Label);
        Assert.Equal("Mesa Boogie Dual Rectifier", line.Body);
    }

    [Fact]
    public void Split_laisse_une_ligne_ordinaire_sans_libelle()
    {
        var line = AnswerLineViewModel.Split("C'est le Drop qui m'a fait craquer.").Single();

        Assert.Equal("", line.Label);
        Assert.Equal("C'est le Drop qui m'a fait craquer.", line.Body);
    }
}
