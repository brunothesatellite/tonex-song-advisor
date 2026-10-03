using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.App.ViewModels;

/// <summary>Database selection plus a read-only summary of what was opened.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IDatabaseHost _host;

    [ObservableProperty]
    private string _databasePath = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string _errorMessage = "";

    /// <summary>True when the last open attempt failed.</summary>
    public bool HasError => ErrorMessage.Length > 0;

    [ObservableProperty]
    private string _formatLabel = "—";

    [ObservableProperty]
    private string _fileLabel = "—";

    [ObservableProperty]
    private string _countsLabel = "—";

    [ObservableProperty]
    private string _settingsLabel = "—";

    [ObservableProperty]
    private string _hashLabel = "—";

    [ObservableProperty]
    private string _tablesLabel = "—";

    public SettingsViewModel(IDatabaseHost host)
        => _host = host ?? throw new ArgumentNullException(nameof(host));

    public string ReadOnlyNotice =>
        "Ouverture strictement en lecture seule : Mode=ReadOnly, PRAGMA query_only=ON et contrôle " +
        "des instructions. Le hachage SHA-256 du fichier est relevé à l'ouverture puis revérifié, " +
        "ce qui prouve que la bibliothèque n'a pas été modifiée.";

    public string FormatHint =>
        "Library.db (V1) contient les 344 colonnes de réglages de chaque preset. " +
        "Library2.db (V2) n'en contient aucune : TONEX y a retiré les valeurs numériques.";

    [RelayCommand]
    private async Task LoadAsync()
    {
        ErrorMessage = "";

        if (string.IsNullOrWhiteSpace(DatabasePath))
        {
            ErrorMessage = "Indiquez le chemin d'un fichier Library.db ou Library2.db.";
            return;
        }

        try
        {
            await _host.LoadDatabaseAsync(DatabasePath.Trim());
            Refresh();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            ClearSummary();
        }
    }

    /// <summary>Re-reads the current database's metadata after a successful load.</summary>
    public void Refresh()
    {
        var database = _host.CurrentDatabase;
        if (database is null)
        {
            ClearSummary();
            return;
        }

        DatabasePath = database.Path;
        FormatLabel = database.Format switch
        {
            DatabaseFormat.V1 => "V1 — Library.db (réglages complets)",
            DatabaseFormat.V2 => "V2 — Library2.db (métadonnées uniquement)",
            _ => "Format non reconnu",
        };

        var info = new FileInfo(database.Path);
        FileLabel = $"{info.Length / 1_048_576d:0.0} Mo · écrit le {info.LastWriteTime:dd/MM/yyyy HH:mm}";
        CountsLabel = $"{database.Count("Presets"):N0} presets · {database.Count("ToneModels"):N0} tone models";
        SettingsLabel = database.HasKnobSettings
            ? "Disponibles — réglages numériques exploitables"
            : "Indisponibles — la génération 2 ne stocke aucun réglage";
        HashLabel = database.Sha256[..16] + "…";
        TablesLabel = string.Join(", ", database.Tables);
        ErrorMessage = "";
    }

    public void ClearSummary()
    {
        FormatLabel = "—";
        FileLabel = "—";
        CountsLabel = "—";
        SettingsLabel = "—";
        HashLabel = "—";
        TablesLabel = "—";
    }

    partial void OnDatabasePathChanged(string value)
    {
        if (_host.CurrentDatabase is null
            || !string.Equals(value, _host.CurrentDatabase.Path, StringComparison.OrdinalIgnoreCase))
        {
            ErrorMessage = "";
        }
    }
}
