using Microsoft.Data.Sqlite;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The whole point of the reader layer: opening a TONEX library must never write to it.
/// </summary>
public class ReadOnlyGuardTests
{
    [LibraryTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ReadingALibrary_LeavesTheFileByteIdentical(bool v1)
    {
        var path = TestPaths.Require(v1 ? TestPaths.V1 : TestPaths.V2);
        var before = FileIntegrity.Sha256(path);

        await Task.Run(() =>
        {
            using var database = ToneXDatabase.Open(path);
            database.LoadPresets();
            database.LoadToneModels();
            Assert.True(database.VerifyUnchanged());
        });

        Assert.Equal(before, FileIntegrity.Sha256(path));
    }

    [Theory]
    [InlineData("UPDATE Presets SET ModelGain = 1")]
    [InlineData("DELETE FROM Presets")]
    [InlineData("INSERT INTO Presets (GUID) VALUES ('x')")]
    [InlineData("DROP TABLE Presets")]
    [InlineData("VACUUM")]
    [InlineData("PRAGMA writable_schema = ON")]
    [InlineData("  \n\t DELETE FROM Presets")]
    [InlineData("-- comment\nDELETE FROM Presets")]
    [InlineData("/* block */ DELETE FROM Presets")]
    public void EnsureReadOnly_RejectsWritingStatements(string sql)
    {
        Assert.Throws<InvalidOperationException>(() => ReadOnlyConnection.EnsureReadOnly(sql));
    }

    [Theory]
    [InlineData("SELECT * FROM Presets")]
    [InlineData("select 1")]
    [InlineData("  SELECT COUNT(*) FROM Presets")]
    [InlineData("WITH x AS (SELECT 1) SELECT * FROM x")]
    [InlineData("EXPLAIN SELECT * FROM Presets")]
    [InlineData("-- a comment\nSELECT 1")]
    public void EnsureReadOnly_AllowsReadingStatements(string sql)
    {
        ReadOnlyConnection.EnsureReadOnly(sql);
    }

    [LibraryFact]
    public void Connection_RejectsAWriteAtTheSqliteLevel()
    {
        var path = TestPaths.Require(TestPaths.V1);
        using var connection = ReadOnlyConnection.Open(path);

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Presets";

        Assert.Throws<SqliteException>(() => command.ExecuteNonQuery());
    }

    [Fact]
    public void Open_NonexistentPath_ThrowsFileNotFound()
    {
        var missing = Path.Combine(TestPaths.WorkspaceRoot, "db", "nope.db");

        Assert.Throws<FileNotFoundException>(() => ReadOnlyConnection.Open(missing));
    }
}
