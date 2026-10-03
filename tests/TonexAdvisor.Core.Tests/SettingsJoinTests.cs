using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// A generation 2 library carries no knob values — they are in IK's encrypted .txp files — but
/// it is usually an upgrade of a generation 1 one, where the values are plain. The join is the
/// only honest way to show them again.
/// </summary>
public class SettingsJoinTests
{
    [LibraryFact]
    public void AGeneration2LibraryBorrowsTheValuesOfItsCompanion()
    {
        var v2 = SampleLibraries.Gen2;
        var v1 = SampleLibraries.Gen1;

        var enriched = SettingsJoin.WithSettingsFrom(v2.Presets, v2.ToneModels, v1.Presets, v1.ToneModels);

        var withSettings = enriched.Count(preset => preset.HasKnobSettings);
        Assert.True(withSettings > 2000, $"only {withSettings} presets got settings");

        // Et chaque valeur empruntée dit d'où elle vient.
        Assert.All(
            enriched.Where(preset => preset.Settings?.Origin.Length > 0),
            preset => Assert.Equal(SettingsJoin.Origin, preset.Settings!.Origin));
    }

    [LibraryFact]
    public void TheValuesAreTheRealOnes_NotInvented()
    {
        var v2 = SampleLibraries.Gen2;
        var v1 = SampleLibraries.Gen1;

        var enriched = SettingsJoin.WithSettingsFrom(v2.Presets, v2.ToneModels, v1.Presets, v1.ToneModels);

        // Un preset que les deux bibliothèques partagent : ses valeurs V1 doivent arriver telles
        // quelles, pas reformées.
        var name = v1.Presets.First(preset => preset.HasKnobSettings).Name;
        var source = v1.Presets.First(preset => preset.Name == name);
        var joined = enriched.FirstOrDefault(preset => preset.Name == name);

        Assert.NotNull(joined?.Settings);
        Assert.Equal(source.Settings!.Number("ModelGain"), joined!.Settings!.Number("ModelGain"));
        Assert.Equal(source.Settings.Number("EqTreble"), joined.Settings.Number("EqTreble"));
    }

    [LibraryFact]
    public void APresetThatOnlyExistsInGeneration2_StaysWithoutSettings()
    {
        var v2 = SampleLibraries.Gen2;
        var v1 = SampleLibraries.Gen1;

        var onlyInV2 = v2.Presets.FirstOrDefault(preset =>
            preset.Name.Length > 0 && v1.Presets.All(other => other.Name != preset.Name));

        if (onlyInV2 is null)
            return; // toutes les bibliothèques ne contiennent que des presets communs

        var enriched = SettingsJoin.WithSettingsFrom(v2.Presets, v2.ToneModels, v1.Presets, v1.ToneModels);
        var joined = enriched.First(preset => preset.Name == onlyInV2.Name);

        Assert.False(joined.HasKnobSettings, $"{joined.Name} should not have borrowed settings");
    }

    [LibraryFact]
    public void AGeneration1LibraryIsLeftAlone()
    {
        var v1 = SampleLibraries.Gen1;

        // Rien à enrichir quand la bibliothèque porte déjà ses valeurs : la source ne doit pas se
        // voir remplacer ses réglages par une copie.
        var enriched = SettingsJoin.WithSettingsFrom(v1.Presets, v1.ToneModels, v1.Presets, v1.ToneModels);

        Assert.Equal(v1.Presets.Count, enriched.Count(preset => preset.HasKnobSettings));
        Assert.All(enriched, preset => Assert.Equal("", preset.Settings?.Origin ?? ""));
    }

    [Fact]
    public void TheCompanionIsLookedForBesideTheLibrary()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tonex-join-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            var v2 = Path.Combine(directory, "Library2.db");
            var v1 = Path.Combine(directory, "Library.db");
            File.WriteAllText(v2, "");
            File.WriteAllText(v1, "");

            Assert.Equal(v1, SettingsJoin.FindCompanionPath(v2));
            Assert.True(SettingsJoin.FindCompanionPath(v1) is null, "a library cannot be its own companion");
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
