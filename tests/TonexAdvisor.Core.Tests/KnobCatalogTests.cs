using TonexAdvisor.Core.Data;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Tests;

public class KnobCatalogTests
{
    [LibraryFact]
    public void Sections_CoverEveryPresetParameterOfAV1Library()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V1));

        var known = KnobCatalog.AllParameters.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unknown = new List<string>();

        foreach (var preset in database.LoadPresets())
        {
            foreach (var name in preset.Settings!.Names)
            {
                if (!known.Contains(name))
                    unknown.Add(name);
            }
        }

        Assert.Empty(unknown.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    [LibraryFact]
    public void AllParameters_HasNoDuplicates()
    {
        var parameters = KnobCatalog.AllParameters;

        Assert.Equal(parameters.Count, parameters.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [LibraryFact]
    public void Sections_ExposeTheSwitchesADetailPanelNeeds()
    {
        foreach (var section in KnobCatalog.Sections)
        {
            if (section.EnableParam is not null)
                Assert.Contains(section.EnableParam, KnobCatalog.AllParameters);

            if (section.PositionParam is not null)
                Assert.Contains(section.PositionParam, KnobCatalog.AllParameters);

            Assert.NotEmpty(section.Knobs);
            Assert.False(string.IsNullOrWhiteSpace(section.Title));
        }
    }

    [LibraryFact]
    public void HardwareSections_AreTheSoftwareOnesWithAPrefix()
    {
        var software = KnobCatalog.Sections.Single(section => section.Key == "eq");
        var hardware = KnobCatalog.HardwareASections.Single(section => section.Key == "eq-HWParamA");

        Assert.Equal(
            software.Knobs.Select(knob => $"HWParamA_{knob.Param}"),
            hardware.Knobs.Select(knob => knob.Param));
        Assert.Equal("HWParamA_EqBass", hardware.Knobs[0].Param);
    }

    [LibraryFact]
    public void Find_ResolvesAParameterByItsToneColumn()
    {
        var knob = KnobCatalog.Find("eqbass");

        Assert.NotNull(knob);
        Assert.Equal("EqBass", knob.Param, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(KnobShape.Knob, knob.Shape);

        Assert.Null(KnobCatalog.Find("NoSuchParam"));
    }

    [LibraryFact]
    public void Find_KeepsSelectorsOnDiscreteParameters()
    {
        foreach (var param in new[] { "ModModel", "DelayModel", "ReverbModel", "VIRCabModel", "VIRCabMic1Model" })
        {
            var knob = KnobCatalog.Find(param);
            Assert.NotNull(knob);
            Assert.Equal(KnobShape.Selector, knob.Shape);
        }
    }
}
