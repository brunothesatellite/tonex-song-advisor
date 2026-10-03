using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The two sample libraries are read once and shared: opening a 78 MB database costs about a
/// second, and the view model tests all ask the same question of the same rows.
/// </summary>
internal static class SampleLibraries
{
    private static readonly Lazy<LibraryIndex> Gen1Index = new(() => Load(TestPaths.V1));
    private static readonly Lazy<LibraryIndex> Gen2Index = new(() => Load(TestPaths.V2));

    /// <summary><c>Library.db</c> — generation 1, with full knob settings.</summary>
    public static LibraryIndex Gen1 => Gen1Index.Value;

    /// <summary><c>Library2.db</c> — generation 2, metadata only.</summary>
    public static LibraryIndex Gen2 => Gen2Index.Value;

    private static LibraryIndex Load(string path)
    {
        using var database = ToneXDatabase.Open(TestPaths.Require(path));
        return new LibraryIndex(database.LoadPresets(), database.LoadToneModels());
    }
}
