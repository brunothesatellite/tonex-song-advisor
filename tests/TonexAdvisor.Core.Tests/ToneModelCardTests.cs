using TonexAdvisor.App.ViewModels;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The tone model card is the only place a capture describes itself in the preset detail: a
/// « rig complet » has no amplifier name, and hiding the stomp there makes the capture mute.
/// </summary>
public class ToneModelCardTests
{
    [Fact]
    public void RigComplet_KeepsItsStompVisible()
    {
        var card = new ToneModelCardViewModel(new ToneModelRecord
        {
            Key = "rig-1",
            Name = "Mon rig",
            StompName = "Maxon OD808",
            Kind = ToneModelKind.ComplexRig,
        });

        Assert.Equal("Maxon OD808", card.AmpLine);
    }

    [Fact]
    public void StompAndAmp_AreShownInTheSignalOrder()
    {
        var card = new ToneModelCardViewModel(new ToneModelRecord
        {
            Key = "sa-1",
            Name = "TS808 + JCM800",
            StompName = "Ibanez TS808",
            AmpName = "Marshall JCM 800",
            Kind = ToneModelKind.StompAndAmp,
        });

        Assert.Equal("Ibanez TS808 -> Marshall JCM 800", card.AmpLine);
    }

    [Fact]
    public void AmpOnly_ShowsTheAmplifierAndItsChannel()
    {
        var card = new ToneModelCardViewModel(new ToneModelRecord
        {
            Key = "amp-1",
            Name = "Plexi",
            AmpName = "Marshall Super Lead",
            AmpChannel = "Lead",
            Kind = ToneModelKind.Amp,
        });

        Assert.Equal("Marshall Super Lead - canal Lead", card.AmpLine);
    }

    [Fact]
    public void NothingNamed_FallsBackOnTheCaptureName()
    {
        var card = new ToneModelCardViewModel(new ToneModelRecord
        {
            Key = "cab-1",
            Name = "Baffle 4x12",
            Kind = ToneModelKind.CustomIR,
        });

        Assert.Equal("Baffle 4x12", card.AmpLine);
    }
}
