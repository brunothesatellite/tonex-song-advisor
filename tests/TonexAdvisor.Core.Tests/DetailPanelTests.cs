using TonexAdvisor.App.ViewModels;
using TonexAdvisor.Core.Data;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The detail panel is the screen this application exists for, so its view model is asserted
/// against the real sample libraries rather than a hand-made fixture.
/// </summary>
public class DetailPanelTests
{
    [LibraryFact]
    public void V1Preset_ExposesSignalPathSections_WithGaugesThatContainTheirValue()
    {
        var index = SampleLibraries.Gen1;
        var preset = index.Presets.Single(p => p.Name == "1CHAIN");

        var detail = PresetDetailViewModel.Create(preset, index.ModelsFor(preset), index.Ranges);

        Assert.True(detail.HasSettings);
        Assert.NotEmpty(detail.Sections);
        Assert.NotEmpty(detail.ToneModels);
        Assert.Empty(detail.SettingsNote);

        var knobs = detail.Sections.SelectMany(section => section.Knobs).ToList();
        Assert.NotEmpty(knobs);

        foreach (var knob in knobs)
        {
            Assert.False(string.IsNullOrWhiteSpace(knob.Label), $"{knob.Param} has no label");
            Assert.InRange(knob.Fraction, 0d, 1d);
            Assert.True(knob.Value >= knob.Minimum, $"{knob.Param}: {knob.Value} < {knob.Minimum}");
            Assert.True(knob.Value <= knob.Maximum, $"{knob.Param}: {knob.Value} > {knob.Maximum}");
        }
    }

    [LibraryFact]
    public void V1Preset_KnownFactoryPreset_ShowsTheRealAmpGain()
    {
        var index = SampleLibraries.Gen1;
        var preset = index.Presets.Single(p => p.Name == "1CHAIN");

        var detail = PresetDetailViewModel.Create(preset, index.ModelsFor(preset), index.Ranges);

        var gain = detail.Sections
            .Where(section => section.Key == "tone")
            .SelectMany(section => section.Knobs)
            .Single(knob => knob.Param == "ModelGain");

        Assert.Equal("Gain", gain.Label);
        Assert.Equal(KnobShape.Knob, gain.Shape);
        Assert.Equal(7.5, gain.Value);
        Assert.True(gain.Minimum < gain.Value);
        Assert.True(gain.Value < gain.Maximum);
    }

    [LibraryFact]
    public void V1Preset_GiveEverySectionAName_AndNeverARepeatedTitle()
    {
        var index = SampleLibraries.Gen1;
        var preset = index.Presets.Single(p => p.Name == "1CHAIN");

        var detail = PresetDetailViewModel.Create(preset, index.ModelsFor(preset), index.Ranges);

        Assert.All(detail.Sections, section =>
        {
            Assert.False(string.IsNullOrWhiteSpace(section.Title));
            Assert.NotEmpty(section.Knobs);
        });

        Assert.Equal(
            detail.Sections.Count,
            detail.Sections.Select(section => section.Title).Distinct(StringComparer.Ordinal).Count());
    }

    [LibraryFact]
    public void BypassedBlocks_AreDimmedButStillListed()
    {
        var index = SampleLibraries.Gen1;
        var found = false;

        foreach (var preset in index.Presets.Take(80))
        {
            var detail = PresetDetailViewModel.Create(preset, index.ModelsFor(preset), index.Ranges);

            foreach (var section in detail.Sections.Where(section => section.HasSwitch && section.BlockEnabled == false))
            {
                found = true;
                Assert.True(section.IsDimmed, $"{section.Title} should be drawn dimmed");
                Assert.Equal("BYPASS", section.SwitchLabel);
                Assert.NotEmpty(section.Knobs);
            }
        }

        Assert.True(found, "expected at least one bypassed block in the sample library");
    }

    [LibraryFact]
    public void V1Preset_HardwareSlots_AreHiddenBehindTheToggle()
    {
        var index = SampleLibraries.Gen1;
        var preset = index.Presets.Single(p => p.Name == "1CHAIN");

        var detail = PresetDetailViewModel.Create(preset, index.ModelsFor(preset), index.Ranges);

        Assert.True(detail.HasHardware);
        Assert.False(detail.ShowHardware);
        Assert.Contains("Afficher", detail.HardwareSummary, StringComparison.Ordinal);

        detail.ShowHardware = true;

        Assert.Contains("Masquer", detail.HardwareSummary, StringComparison.Ordinal);
    }

    [LibraryFact]
    public void V2Preset_ExplainsMissingKnobsInsteadOfShowingAnEmptyPanel()
    {
        var index = SampleLibraries.Gen2;
        var preset = index.Presets.Single(p => p.Name == "80s Clean");

        var detail = PresetDetailViewModel.Create(preset, index.ModelsFor(preset), index.Ranges);

        Assert.False(detail.HasSettings);
        Assert.Empty(detail.Sections);
        Assert.Empty(detail.HardwareASections);
        Assert.False(detail.HasHardware);
        Assert.Contains("génération 2", detail.SettingsNote, StringComparison.Ordinal);

        Assert.True(detail.HasChain);
        Assert.NotEqual("—", detail.ChainSummary);
        Assert.Contains("actif", detail.ActiveBlockLine, StringComparison.Ordinal);
        Assert.NotEmpty(detail.ToneModels);
    }

    [LibraryFact]
    public void ToneModelDetail_ListsThePresetsThatUseIt()
    {
        var index = SampleLibraries.Gen1;
        var model = index.ToneModels.First(candidate =>
            candidate.AmpName.Length > 0 && index.PresetsFor(candidate.Key).Count > 0);

        var detail = new ToneModelDetailViewModel(model, index.PresetsFor(model.Key));

        Assert.NotEmpty(detail.Presets);
        Assert.Equal(detail.Presets.Count, detail.PresetCount);
        Assert.NotEmpty(detail.PresetNames);
        Assert.False(string.IsNullOrWhiteSpace(detail.AmpName));
        Assert.False(string.IsNullOrWhiteSpace(detail.Name));
    }
}
