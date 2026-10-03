using TonexAdvisor.App.Config;
using TonexAdvisor.App.Services;
using TonexAdvisor.App.ViewModels;
using TonexAdvisor.Core.Advice;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The non-negotiable constraint, asserted on a whole session rather than on a single call:
/// reading, scoring, advising and clicking through the UI must leave the TONEX libraries byte
/// for byte as they were, without growing a SQLite side file beside them.
/// </summary>
public class ReadOnlyIntegrityTests
{
    [LibraryFact]
    public void AFullSession_NeverChangesTheLibraries()
    {
        var before = Snapshot();

        // 1. Reading, indexing and advising.
        using (var database = ToneXDatabase.Open(TestPaths.V1))
        {
            var index = new LibraryIndex(database.LoadPresets(), database.LoadToneModels());
            var advisor = new LibraryAdvisor(index);

            var query = new AdviceQuery { Artist = "ACDZ", Song = "Back", Style = "metal" };
            var presets = advisor.RankPresets(query, 3);
            var combinations = advisor.RankCombinations(query, 3);
            CataloguePrompt.Build(query, index);
        }

        // 2. A UI session: open, filter, reset, ask for advice, open a recommendation.
        var viewModel = new LibraryViewModel(() => new AppConfig(), new InMemoryUserStateStore());
        viewModel.Attach(SampleLibraries.Gen1);

        viewModel.SearchText = "metal";
        viewModel.OnlyFavorites = true;
        viewModel.ClearFiltersCommand.Execute(null);

        viewModel.Advice.Style = "metal";
        viewModel.Advice.AdviseCommand.Execute(null);

        Assert.NotEmpty(viewModel.Advice.Presets);
        viewModel.Advice.Presets[0].OpenCommand.Execute(null);

        Assert.Equal(before, Snapshot());
    }

    [LibraryFact]
    public void AReadOnlySession_LeavesNoRollbackJournalAndNoPendingPages()
    {
        // The libraries are in WAL mode: simply opening one materialises a `-shm` index and an
        // empty `-wal` beside it, which is SQLite bookkeeping rather than a modification. What
        // must never happen is uncommitted data waiting in a journal or in a write-ahead log.
        var sideFiles = new[]
        {
            TestPaths.V1 + "-wal",
            TestPaths.V1 + "-shm",
            TestPaths.V1 + "-journal",
            TestPaths.V2 + "-wal",
            TestPaths.V2 + "-shm",
            TestPaths.V2 + "-journal",
        };

        foreach (var file in sideFiles)
            if (file.EndsWith("-journal", StringComparison.Ordinal))
                Assert.False(File.Exists(file), $"{file} should not exist");

        using (var database = ToneXDatabase.Open(TestPaths.V1))
        {
            database.LoadPresets();
            database.LoadToneModels();
        }

        foreach (var file in sideFiles)
        {
            if (file.EndsWith("-journal", StringComparison.Ordinal))
            {
                Assert.False(File.Exists(file), $"{file} appeared next to the library");
                continue;
            }

            if (file.EndsWith("-wal", StringComparison.Ordinal) && File.Exists(file))
                Assert.Equal(0, new FileInfo(file).Length);
        }
    }

    private static Dictionary<string, string> Snapshot()
    {
        var paths = new[] { TestPaths.V1, TestPaths.V2 };
        return paths.ToDictionary(path => path, FileIntegrity.Sha256);
    }
}
