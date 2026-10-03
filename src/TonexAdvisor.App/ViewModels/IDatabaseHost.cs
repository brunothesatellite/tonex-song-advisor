using TonexAdvisor.Core.Data;

namespace TonexAdvisor.App.ViewModels;

/// <summary>Implemented by the shell so child pages can ask for a different database.</summary>
public interface IDatabaseHost
{
    /// <summary>The library currently open, or null.</summary>
    ToneXDatabase? CurrentDatabase { get; }

    /// <summary>Opens <paramref name="path"/> off the UI thread. Throws on failure.</summary>
    Task LoadDatabaseAsync(string path);
}
