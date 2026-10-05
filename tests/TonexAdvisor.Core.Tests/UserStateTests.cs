using TonexAdvisor.App.Services;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Filters are the only thing the browser has to remember: they live next to the API
/// configuration, outside the repository, and a broken file must never block the start-up.
/// </summary>
public class UserStateTests
{
    [Fact]
    public void SaveThenLoad_RoundTripsEveryPreference()
    {
        var file = TempFile();
        try
        {
            new UserState
            {
                SearchText = "metal",
                Category = "HI-GAIN",
                Genre = "Metal",
                Folder = "GalTone",
                OnlyFavorites = true,
                SelectedTabIndex = 2,
                UseTonexLibrary = true,
                DatabasePath = @"D:\bibliotheques\Library.db",
                TonexDatabasePath = @"C:\TONEX\Library2.db",
                UiLanguage = "en",
            }.Save(file);

            var loaded = UserState.Load(file);

            Assert.Equal("metal", loaded.SearchText);
            Assert.Equal("HI-GAIN", loaded.Category);
            Assert.Equal("Metal", loaded.Genre);
            Assert.Equal("GalTone", loaded.Folder);
            Assert.True(loaded.OnlyFavorites);
            Assert.Equal(2, loaded.SelectedTabIndex);
            Assert.True(loaded.UseTonexLibrary);
            Assert.Equal(@"D:\bibliotheques\Library.db", loaded.DatabasePath);
            Assert.Equal(@"C:\TONEX\Library2.db", loaded.TonexDatabasePath);
            Assert.Equal("en", loaded.UiLanguage);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Load_MissingOrBrokenFile_YieldsEmptyPreferences()
    {
        var missing = TempFile();
        var fresh = UserState.Load(missing);

        Assert.Equal("", fresh.SearchText);
        Assert.Equal("", fresh.Category);
        Assert.False(fresh.OnlyFavorites);
        Assert.Equal(0, fresh.SelectedTabIndex);

        var broken = TempFile();
        try
        {
            File.WriteAllText(broken, "{ pas du json");
            var loaded = UserState.Load(broken);

            Assert.Equal("", loaded.SearchText);
            Assert.False(loaded.OnlyFavorites);
        }
        finally
        {
            File.Delete(broken);
        }
    }

    [Fact]
    public void PreferencesLiveOutsideTheRepository()
    {
        Assert.Contains("TonexAdvisor", UserState.DefaultPath, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("state.json", UserState.DefaultPath, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(AppConfigPath(), Path.GetDirectoryName(UserState.DefaultPath));
    }

    [Fact]
    public void SaveThenLoad_RoundTripsTheChosenColumnWidths()
    {
        var file = TempFile();
        try
        {
            new UserState
            {
                PresetColumnWidths = new Dictionary<string, double> { ["Nom"] = 200, ["Stomp"] = 175 },
                ToneModelColumnWidths = new Dictionary<string, double> { ["Ampli"] = 180 },
            }.Save(file);

            var loaded = UserState.Load(file);

            Assert.Equal(200, loaded.PresetColumnWidths["Nom"]);
            Assert.Equal(175, loaded.PresetColumnWidths["Stomp"]);
            Assert.Equal(180, loaded.ToneModelColumnWidths["Ampli"]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Clone_HandsOutItsOwnColumnWidths()
    {
        var original = new UserState
        {
            UiLanguage = "en",
            PresetColumnWidths = new Dictionary<string, double> { ["Nom"] = 200 },
        };

        var copy = original.Clone();
        copy.PresetColumnWidths["Nom"] = 999;
        copy.UiLanguage = "fr";

        Assert.Equal(200, original.PresetColumnWidths["Nom"]);
        Assert.Equal("en", original.UiLanguage);
    }

    [Fact]
    public void AV121FileWithoutTheLanguageField_DeserializesToNeverChosen()
    {
        var file = TempFile();
        try
        {
            // Blob v1.2.1 : les douze champs de l'époque, aucun champ UiLanguage — §10.
            File.WriteAllText(file, """
                {
                  "SearchText": "metal",
                  "DisabledVoices": [],
                  "UseTonexLibrary": false,
                  "DatabasePath": "D:/bibliotheques/Library.db",
                  "TonexDatabasePath": "",
                  "Category": "HI-GAIN",
                  "Genre": "Metal",
                  "Folder": "",
                  "OnlyFavorites": true,
                  "SelectedTabIndex": 1,
                  "PresetColumnWidths": { "Nom": 200 },
                  "ToneModelColumnWidths": { "Stomp": 175 }
                }
                """);

            var loaded = UserState.Load(file);

            // Null = jamais choisi : la prochaine lecture détectera la langue du système.
            Assert.Null(loaded.UiLanguage);
            Assert.Equal("metal", loaded.SearchText);
            Assert.Equal(1, loaded.SelectedTabIndex);
            Assert.Equal(200, loaded.PresetColumnWidths["Nom"]);
            Assert.Equal(175, loaded.ToneModelColumnWidths["Stomp"]);
        }
        finally
        {
            File.Delete(file);
        }
    }

    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), "tonex-state-" + Guid.NewGuid().ToString("N") + ".json");

    private static string AppConfigPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TonexAdvisor");
}
