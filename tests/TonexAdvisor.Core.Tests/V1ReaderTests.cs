using TonexAdvisor.Core.Data;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Tests;

public class V1ReaderTests
{
    [Fact]
    public void Open_DetectsV1_AndExposesKnobSettings()
    {
        var path = TestPaths.Require(TestPaths.V1);

        using var database = ToneXDatabase.Open(path);

        Assert.Equal(DatabaseFormat.V1, database.Format);
        Assert.True(database.HasKnobSettings);
        Assert.Contains("Presets", database.Tables, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void LoadPresets_ReturnsEveryRow_WithToneModelLinkAndSettings()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V1));

        var presets = database.LoadPresets();

        Assert.Equal(database.Count("Presets"), presets.Count);
        Assert.All(presets, preset =>
        {
            Assert.False(string.IsNullOrWhiteSpace(preset.Key), "preset without a key");
            Assert.False(string.IsNullOrWhiteSpace(preset.Name), "preset without a name");
            Assert.True(preset.HasKnobSettings, $"{preset.Name}: missing knob settings");
            Assert.NotEmpty(preset.ToneModelKeys);
            Assert.Empty(preset.Chain);
        });
    }

    [Fact]
    public void LoadPresets_KnownFactoryPreset_CarriesExpectedValues()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V1));

        var preset = database.LoadPresets().Single(p => p.Name == "1CHAIN");

        Assert.Equal("IK Multimedia", preset.UserName);
        Assert.Equal("HI-GAIN", preset.Category);
        Assert.Single(preset.ToneModelKeys);

        var settings = preset.Settings!;
        Assert.True(settings.IsEnabled("ModelEnable"));
        Assert.Equal(7.5, settings.Number("ModelGain"));
        Assert.Equal(100.0, settings.Number("ModelMix"));
        Assert.Equal(5.0, settings.Number("EqBass"));
        Assert.Equal(5.0, settings.Number("EqMid"));
        Assert.Equal(5.0, settings.Number("EqTreble"));
        Assert.Equal(25.0, settings.Number("VIRCabModel"));
    }

    [Fact]
    public void LoadPresets_CollectsFolders()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V1));

        var withFolder = database.LoadPresets().Where(p => p.Folders.Count > 0).ToList();

        Assert.NotEmpty(withFolder);
    }

    [Fact]
    public void LoadToneModels_ReturnsEveryRow_WithMetadataButNoPayload()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V1));

        var models = database.LoadToneModels();

        Assert.Equal(database.Count("ToneModels"), models.Count);
        Assert.All(models, model =>
        {
            Assert.False(string.IsNullOrWhiteSpace(model.Key), "model without a key");
            Assert.True(Enum.IsDefined(model.Kind), $"unknown kind for {model.Name}");
        });
    }

    [Fact]
    public void LoadToneModels_ResolvesAuthorAndFolder()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V1));

        var models = database.LoadToneModels();

        Assert.Contains(models, model => model.Folders.Count > 0);
        Assert.Contains(models, model => model.CabName.Length > 0);
        Assert.Contains(models, model => model.Mic1.Length > 0);
    }

    [Fact]
    public void LoadToneModels_KnownModel_CarriesAmpAndCab()
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(TestPaths.V1));

        var models = database.LoadToneModels();
        var sample = models.Single(m => m.Key == "121f0d79-c52f-4a9f-13c8-4c0f7d84ad66");

        Assert.Equal("EVH", sample.Skin);
        Assert.Equal(ToneModelKind.StompAndAmp, sample.Kind);
        Assert.Equal("4 - StompAndAmp", sample.KindOrder);
    }
}
