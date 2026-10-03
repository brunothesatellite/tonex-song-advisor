using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The TONEX folder is found through the Windows « Documents » known folder, which follows
/// OneDrive redirections — and only its root is listed, since Backup holds copies, not the
/// library in use.
/// </summary>
public class TonexLibraryFolderTests
{
    private static string NewFolder()
        => Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "tonex-folder-" + Guid.NewGuid().ToString("N"))).FullName;

    [Fact]
    public void OnlyRootDatabasesAreListed_MostRecentFirst()
    {
        var folder = NewFolder();

        try
        {
            var older = Path.Combine(folder, "Library.db");
            var newer = Path.Combine(folder, "Library2.db");
            File.WriteAllText(older, "");
            File.WriteAllText(newer, "");
            File.SetLastWriteTime(older, new DateTime(2026, 1, 1, 12, 0, 0));
            File.SetLastWriteTime(newer, new DateTime(2026, 2, 1, 12, 0, 0));

            Directory.CreateDirectory(Path.Combine(folder, "Backup"));
            File.WriteAllText(Path.Combine(folder, "Backup", "Library.db"), "");
            File.WriteAllText(Path.Combine(folder, "notes.txt"), "");

            var libraries = TonexLibraryFolder.FindDatabases(folder);

            Assert.Equal(2, libraries.Count);
            Assert.Equal("Library2.db", libraries[0].Name);
            Assert.Equal("V2", libraries[0].Generation);
            Assert.Equal("Library.db", libraries[1].Name);
            Assert.Equal("V1", libraries[1].Generation);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void AnAbsentFolderSimplyHoldsNothing()
    {
        var missing = Path.Combine(Path.GetTempPath(), "tonex-absent-" + Guid.NewGuid().ToString("N"));

        Assert.Empty(TonexLibraryFolder.FindDatabases(missing));
    }

    [Fact]
    public void AnEmptyFolderOffersNothingToChoose()
    {
        var folder = NewFolder();

        try
        {
            Assert.Empty(TonexLibraryFolder.FindDatabases(folder));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData("Library2.db", "V2")]
    [InlineData("Library.db", "V1")]
    [InlineData("Backup2.db", "V2")]
    [InlineData("Autre.db", "V1")]
    public void TheGenerationComesFromTheFileName(string fileName, string expected)
    {
        Assert.Equal(expected, TonexLibraryFolder.GenerationOf(fileName));
    }

    [Fact]
    public void TheOfficialFolderIsTheDocumentsKnownFolder()
    {
        // Pas %USERPROFILE%\Documents : « Documents » peut être redirigé vers OneDrive, et c'est
        // le dossier connu de Windows que TONEX utilise.
        Assert.EndsWith(
            Path.Combine("IK Multimedia", "TONEX"),
            TonexLibraryFolder.DefaultPath,
            StringComparison.OrdinalIgnoreCase);
    }
}
