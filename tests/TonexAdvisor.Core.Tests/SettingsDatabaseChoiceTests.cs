using TonexAdvisor.App.Services;
using TonexAdvisor.App.ViewModels;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Two ways of choosing the library: a path, or the TONEX folder. Ticking the box greys out the
/// other one, and the choice is remembered — without ever touching the user's real files.
/// </summary>
public class SettingsDatabaseChoiceTests
{
    private sealed class FakeHost : IDatabaseHost
    {
        public List<string> Loaded { get; } = new();

        public ToneXDatabase? CurrentDatabase => null;

        public Task LoadDatabaseAsync(string path)
        {
            Loaded.Add(path);
            return Task.CompletedTask;
        }
    }

    private static string NewFolder()
    {
        var folder = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "tonex-choice-" + Guid.NewGuid().ToString("N"))).FullName;

        var older = Path.Combine(folder, "Library.db");
        var newer = Path.Combine(folder, "Library2.db");
        File.WriteAllText(older, "");
        File.WriteAllText(newer, "");
        File.SetLastWriteTime(older, new DateTime(2026, 1, 1, 12, 0, 0));
        File.SetLastWriteTime(newer, new DateTime(2026, 2, 1, 12, 0, 0));

        return folder;
    }

    [Fact]
    public void TickingTheBox_GreysOutThePathAndOpensTheSelectedLibrary()
    {
        var folder = NewFolder();

        try
        {
            var host = new FakeHost();
            var viewModel = new SettingsViewModel(host, new InMemoryUserStateStore(), folder);
            viewModel.DatabasePath = @"D:\ailleurs\Library.db";

            Assert.True(viewModel.CanUseTonex);
            Assert.True(viewModel.IsManualEnabled);
            Assert.False(viewModel.IsTonexEnabled);

            viewModel.UseTonexLibrary = true;

            Assert.False(viewModel.IsManualEnabled, "choice 1 must be greyed out");
            Assert.True(viewModel.IsTonexEnabled);
            Assert.True(host.Loaded.Count > 0);
            Assert.EndsWith("Library2.db", host.Loaded[^1], StringComparison.OrdinalIgnoreCase);

            // Et en décochant, on repart sur le chemin — sans clic supplémentaire.
            viewModel.UseTonexLibrary = false;

            Assert.True(viewModel.IsManualEnabled);
            Assert.EndsWith(@"D:\ailleurs\Library.db", host.Loaded[^1], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void ChangingTheSelectionInList_LoadsThatLibrary()
    {
        var folder = NewFolder();

        try
        {
            var host = new FakeHost();
            var viewModel = new SettingsViewModel(host, new InMemoryUserStateStore(), folder);
            viewModel.UseTonexLibrary = true;

            var older = viewModel.TonexLibraries.First(library => library.Name == "Library.db");
            viewModel.SelectedTonexLibrary = older;

            Assert.EndsWith("Library.db", host.Loaded[^1], StringComparison.OrdinalIgnoreCase);
            Assert.Equal("V1", older.Generation);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void TheChoiceIsRemembered()
    {
        var folder = NewFolder();

        try
        {
            var store = new InMemoryUserStateStore();

            var first = new SettingsViewModel(new FakeHost(), store, folder);
            first.UseTonexLibrary = true;

            var again = new SettingsViewModel(new FakeHost(), store, folder);

            Assert.True(again.UseTonexLibrary);
            Assert.NotNull(again.SelectedTonexLibrary);
            Assert.Equal(first.SelectedTonexLibrary?.Path, again.SelectedTonexLibrary?.Path);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnEmptyFolder_DisablesTheBoxAndSaysSo()
    {
        var folder = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "tonex-empty-" + Guid.NewGuid().ToString("N"))).FullName;

        try
        {
            var viewModel = new SettingsViewModel(new FakeHost(), new InMemoryUserStateStore(), folder);

            Assert.False(viewModel.CanUseTonex);
            Assert.Contains("Aucune base trouvée", viewModel.TonexFolderHint, StringComparison.Ordinal);
            Assert.True(viewModel.IsManualEnabled);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
