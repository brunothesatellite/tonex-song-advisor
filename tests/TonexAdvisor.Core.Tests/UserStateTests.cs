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

    private static string TempFile()
        => Path.Combine(Path.GetTempPath(), "tonex-state-" + Guid.NewGuid().ToString("N") + ".json");

    private static string AppConfigPath()
        => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TonexAdvisor");
}
