namespace TonexAdvisor.Core.Data;

/// <param name="Path">Full path of the database file.</param>
/// <param name="Name">File name, what the list shows first.</param>
/// <param name="Generation">« V1 » or « V2 », from the file name convention.</param>
/// <param name="Length">Size in bytes.</param>
/// <param name="Modified">Last write time.</param>
public sealed record LibraryFile(string Path, string Name, string Generation, long Length, DateTime Modified)
{
    public string SizeLabel => $"{Length / 1_048_576d:0.0} Mo";

    public string ModifiedLabel => Modified.ToString("dd/MM/yyyy HH:mm");

    /// <summary>One readable line per library, so the list says what it is opening.</summary>
    public string Label => $"{Name}  ·  {Generation}  ·  {SizeLabel}  ·  {ModifiedLabel}";

    /// <summary>What the drop-down shows.</summary>
    public override string ToString() => Label;
}

/// <summary>
/// Where TONEX keeps its libraries, and what sits there.
/// </summary>
/// <remarks>
/// The folder is found through the Windows « Documents » known folder, not through
/// <c>%USERPROFILE%\Documents</c>: on a machine whose Documents is redirected to OneDrive — the
/// usual case — the naive path does not exist. Only the root of the folder is listed: the
/// <c>Backup</c> sub-folder holds copies, not the library in use.
/// </remarks>
public static class TonexLibraryFolder
{
    /// <summary>TONEX's own folder, wherever the user's Documents happens to live.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "IK Multimedia",
        "TONEX");

    /// <summary>
    /// The database files at the root of <paramref name="folder"/>, most recently written first.
    /// An absent folder simply holds nothing — the caller decides what to say about it.
    /// </summary>
    public static IReadOnlyList<LibraryFile> FindDatabases(string? folder = null)
    {
        var directory = folder ?? DefaultPath;

        if (!Directory.Exists(directory))
            return Array.Empty<LibraryFile>();

        return new DirectoryInfo(directory)
            .EnumerateFiles("*.db", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new LibraryFile(
                file.FullName,
                file.Name,
                GenerationOf(file.Name),
                file.Length,
                file.LastWriteTime))
            .ToList();
    }

    /// <summary>
    /// The generation a file name announces: TONEX writes <c>Library.db</c> for generation 1 and
    /// <c>Library2.db</c> for generation 2. The format is confirmed when the file is opened —
    /// reading every candidate just to label it would open databases for nothing.
    /// </summary>
    public static string GenerationOf(string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return stem.EndsWith("2", StringComparison.Ordinal) ? "V2" : "V1";
    }
}
