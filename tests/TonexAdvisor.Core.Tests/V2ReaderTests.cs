using TonexAdvisor.Core.Data;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Tests;

public class V2ReaderTests
{
    [LibraryFact]
    public void Open_DetectsV2_AndReportsNoKnobSettings()
    {
        var path = TestPaths.Require(TestPaths.V2);

        using var database = ToneXDatabase.Open(path);

        Assert.Equal(DatabaseFormat.V2, database.Format);
        Assert.False(database.HasKnobSettings);
    }

    [LibraryFact]
    public void LoadPresets_ReturnsEveryRow_WithoutSettingsButWithChain()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V2));

        var presets = database.LoadPresets();

        Assert.Equal(database.Count("Presets"), presets.Count);
        Assert.All(presets, preset =>
        {
            Assert.False(string.IsNullOrWhiteSpace(preset.Name), "preset without a name");
            Assert.False(preset.HasKnobSettings, "V2 must not pretend to have knob values");
            Assert.Null(preset.Settings);
            Assert.True(preset.Chain.Count > 0, $"{preset.Name}: empty chain");
        });
    }

    [LibraryFact]
    public void LoadPresets_LinksEveryPresetToOneOrTwoToneModels()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V2));

        var presets = database.LoadPresets();

        Assert.All(presets, preset =>
        {
            Assert.NotEmpty(preset.ToneModelKeys);
            Assert.True(preset.ToneModelKeys.Count <= 2, $"{preset.Name}: unexpected model count");
        });
    }

    [LibraryFact]
    public void LoadPresets_KnownPreset_ParsesChainAsExpected()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V2));

        var preset = database.LoadPresets().Single(p => p.Name == "80s Clean");

        Assert.Equal("CLEAN", preset.Category);
        Assert.Equal("Electric Guitar", preset.Instrument);

        var chain = preset.Chain;
        Assert.Equal(7, chain.Count);
        Assert.Contains(chain, block => block.Id == 0 && block.Bypass);
        Assert.Contains(chain, block => block.Id == 1 && !block.Bypass);
        Assert.Contains(chain, block => block.Id == 3 && !block.Bypass);
        Assert.Equal(6, preset.ActiveChain.Count());
    }

    [LibraryFact]
    public void LoadPresets_PreservesEmptyPaddingSlots()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V2));

        var preset = database.LoadPresets().Single(p => p.Name == "TONEX Board Default");

        Assert.Equal(12, preset.Chain.Count);
        Assert.All(preset.Chain.Where(block => block.Id == -1), block => Assert.True(block.Bypass));
        Assert.Equal(5, preset.ActiveChain.Count());
    }

    [LibraryFact]
    public void LoadToneModels_ReturnsEveryRow_WithAmpAndCabMetadata()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V2));

        var models = database.LoadToneModels();

        Assert.Equal(database.Count("ToneModels"), models.Count);
        Assert.Contains(models, model => model.AmpName.Length > 0);
        Assert.Contains(models, model => model.CabName.Length > 0);
        Assert.Contains(models, model => model.Mic1.Length > 0);
    }

    [LibraryFact]
    public void LoadToneModels_KeyedById_SoPresetLinksResolve()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V2));

        var models = database.LoadToneModels();
        var keys = models.Select(model => model.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var presetKeys = database.LoadPresets().SelectMany(preset => preset.ToneModelKeys);

        Assert.All(presetKeys, key => Assert.Contains(key, keys));
    }

    [LibraryFact]
    public void LoadToneModels_KnownModel_CarriesExpectedMetadata()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V2));

        var model = database.LoadToneModels().Single(m => m.Key == "1");

        Assert.Equal("002df401-c8fb-7ccb-fef1-cddb7b32c18c", model.Guid);
        Assert.Equal("ThreeSixty Turn", model.Name);
        Assert.Equal("Mezzabarba Trinity", model.AmpName);
        Assert.Equal("MXR Overdrive", model.StompName);
        Assert.Equal("Bogner 412", model.CabName);
        Assert.Equal("SM57", model.Mic1);
        Assert.Equal("HI-GAIN", model.Category);
        Assert.Equal(ToneModelKind.ComplexRig, model.Kind);
    }
}
