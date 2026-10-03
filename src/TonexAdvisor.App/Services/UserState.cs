using System.Text.Json;

namespace TonexAdvisor.App.Services;

/// <summary>
/// What the browser remembers between two runs: the filters and the active tab.
/// </summary>
/// <remarks>
/// Favorites are deliberately absent: they belong to the TONEX library, are read from it at
/// every load, and the databases are never written to. This file holds UI preferences only — no
/// secret — and lives next to the API configuration, outside the repository.
/// </remarks>
public sealed class UserState
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string SearchText { get; set; } = "";

    // ── Base de données ────────────────────────────────────────────────────

    /// <summary>True when the library comes from the TONEX folder rather than from a path.</summary>
    public bool UseTonexLibrary { get; set; }

    /// <summary>The path of choice 1.</summary>
    public string DatabasePath { get; set; } = "";

    /// <summary>The library selected in the TONEX folder list, choice 2.</summary>
    public string TonexDatabasePath { get; set; } = "";

    /// <summary>Empty means « every category ».</summary>
    public string Category { get; set; } = "";

    public string Genre { get; set; } = "";

    public string Folder { get; set; } = "";

    public bool OnlyFavorites { get; set; }

    /// <summary>0 presets, 1 tone models, 2 advice.</summary>
    public int SelectedTabIndex { get; set; }

    /// <summary>Next to the API configuration, outside any repository.</summary>
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TonexAdvisor",
        "state.json");

    /// <summary>Reads the state; an absent or broken file yields empty preferences.</summary>
    public static UserState Load(string? path = null)
    {
        var file = path ?? DefaultPath;

        try
        {
            if (!File.Exists(file))
                return new UserState();

            return JsonSerializer.Deserialize<UserState>(File.ReadAllText(file), Options) ?? new UserState();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new UserState();
        }
    }

    /// <summary>
    /// Copies this state. Screens modify their own read, never the stored one: the file store
    /// hands out a fresh object on every read, and the in-memory one must behave the same way.
    /// </summary>
    public UserState Clone() => (UserState)MemberwiseClone();

    /// <summary>Writes the state in place, creating the folder when needed.</summary>
    public void Save(string? path = null)
    {
        var file = path ?? DefaultPath;
        var directory = Path.GetDirectoryName(file);

        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(file, JsonSerializer.Serialize(this, Options));
    }
}
