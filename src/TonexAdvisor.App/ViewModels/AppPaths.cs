namespace TonexAdvisor.App.ViewModels;

/// <summary>Locates a sensible default library when the application starts.</summary>
internal static class AppPaths
{
    /// <summary>
    /// Walks up from the executable looking for a <c>db/Library.db</c> folder, which is where
    /// the development workspace keeps its sample libraries. Returns null when there is none,
    /// so the settings page can ask the user instead.
    /// </summary>
    public static string? FindDefaultDatabase()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "db", "Library.db");
            if (File.Exists(candidate))
                return candidate;

            directory = directory.Parent;
        }

        return null;
    }
}
