using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

public class SchemaSnifferTests
{
    [LibraryFact]
    public void Detect_ClassicLibrary_ReportsV1()
    {
        var path = TestPaths.Require(TestPaths.V1);

        Assert.Equal(DatabaseFormat.V1, SchemaSniffer.Detect(path));
    }

    [LibraryFact]
    public void Detect_Generation2Library_ReportsV2()
    {
        var path = TestPaths.Require(TestPaths.V2);

        Assert.Equal(DatabaseFormat.V2, SchemaSniffer.Detect(path));
    }

    [LibraryFact]
    public void Detect_MissingFile_Throws()
    {
        var missing = Path.Combine(TestPaths.WorkspaceRoot, "db", "does-not-exist.db");

        Assert.Throws<FileNotFoundException>(() => SchemaSniffer.Detect(missing));
    }

    [LibraryFact]
    public void ColumnsOf_V1PresetTable_ContainsToneModelGuid()
    {
        var path = TestPaths.Require(TestPaths.V1);
        using var connection = ReadOnlyConnection.Open(path);

        var columns = SchemaSniffer.ColumnsOf(connection, "Presets");

        Assert.Contains("ToneModel_GUID", columns);
        Assert.Contains("ModelGain", columns);
        Assert.DoesNotContain("Chain", columns);
    }

    [LibraryFact]
    public void ColumnsOf_V2PresetTable_ContainsChainAndNoParameters()
    {
        var path = TestPaths.Require(TestPaths.V2);
        using var connection = ReadOnlyConnection.Open(path);

        var columns = SchemaSniffer.ColumnsOf(connection, "Presets");

        Assert.Contains("Chain", columns);
        Assert.Contains("Tag_PresetName", columns);
        Assert.DoesNotContain("ToneModel_GUID", columns);
        Assert.DoesNotContain("ModelGain", columns);
    }

    [LibraryFact]
    public void ColumnsOf_UnknownTable_ReturnsEmptySet()
    {
        var path = TestPaths.Require(TestPaths.V1);
        using var connection = ReadOnlyConnection.Open(path);

        Assert.Empty(SchemaSniffer.ColumnsOf(connection, "NoSuchTable"));
        Assert.Empty(SchemaSniffer.ColumnsOf(connection, ""));
    }

    [LibraryFact]
    public void TablesOf_BothLibraries_ExposeCoreTables()
    {
        foreach (var path in new[] { TestPaths.V1, TestPaths.V2 })
        {
            TestPaths.Require(path);
            using var connection = ReadOnlyConnection.Open(path);

            var tables = SchemaSniffer.TablesOf(connection);

            Assert.Contains("Presets", tables, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("ToneModels", tables, StringComparer.OrdinalIgnoreCase);
        }
    }
}
