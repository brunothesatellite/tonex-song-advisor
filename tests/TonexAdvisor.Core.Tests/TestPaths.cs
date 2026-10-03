namespace TonexAdvisor.Core.Tests;

/// <summary>Locates the two sample libraries shipped in the workspace <c>db</c> folder.</summary>
internal static class TestPaths
{
    private static readonly Lazy<string> Root = new(FindWorkspaceRoot);

    public static string WorkspaceRoot => Root.Value;

    /// <summary><c>Library.db</c> - generation 1.</summary>
    public static string V1 => Path.Combine(WorkspaceRoot, "db", "Library.db");

    /// <summary><c>Library2.db</c> - generation 2.</summary>
    public static string V2 => Path.Combine(WorkspaceRoot, "db", "Library2.db");

    public static string Require(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Sample library missing at '{path}'. Tests expect the two databases copied " +
                "into the workspace db folder.",
                path);
        }

        return path;
    }

    private static string FindWorkspaceRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "db", "Library.db")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the workspace root containing db\\Library.db.");
    }
}
