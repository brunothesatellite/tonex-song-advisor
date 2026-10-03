namespace TonexAdvisor.Core.Tests;

/// <summary>
/// A fact that needs the two sample libraries.
/// </summary>
/// <remarks>
/// The databases are personal and deliberately not versioned, so a clean checkout (CI) has none:
/// those tests are then <b>skipped</b> rather than failed, and the rest of the suite still
/// builds and runs. On a machine that has the libraries, nothing is skipped.
/// </remarks>
public sealed class LibraryFactAttribute : FactAttribute
{
    public LibraryFactAttribute()
    {
        if (!TestPaths.Exists)
            Skip = "Sample libraries absent (db\\Library.db) : test ignoré.";
    }
}

/// <summary>Same as <see cref="LibraryFactAttribute"/>, for parametrised tests.</summary>
public sealed class LibraryTheoryAttribute : TheoryAttribute
{
    public LibraryTheoryAttribute()
    {
        if (!TestPaths.Exists)
            Skip = "Sample libraries absent (db\\Library.db) : test ignoré.";
    }
}
