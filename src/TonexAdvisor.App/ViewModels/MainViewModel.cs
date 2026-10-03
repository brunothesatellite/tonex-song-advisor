using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.App.ViewModels;

/// <summary>The application shell: navigation, status bar and the open database.</summary>
public partial class MainViewModel : ViewModelBase, IDatabaseHost
{
    private const string SectionLibrary = "library";
    private const string SectionSettings = "settings";

    private ToneXDatabase? _currentDatabase;

    [ObservableProperty]
    private ViewModelBase _currentPage;

    [ObservableProperty]
    private string _currentSection = SectionLibrary;

    [ObservableProperty]
    private string _statusMessage = "Aucune bibliothèque chargée.";

    [ObservableProperty]
    private bool _isBusy;

    public MainViewModel()
    {
        Library = new LibraryViewModel();
        Settings = new SettingsViewModel(this);
        _currentPage = Library;
    }

    public LibraryViewModel Library { get; }

    public SettingsViewModel Settings { get; }

    public ToneXDatabase? CurrentDatabase => _currentDatabase;

    public bool IsLibrarySection => CurrentSection == SectionLibrary;

    public bool IsSettingsSection => CurrentSection == SectionSettings;

    public bool HasDatabase => _currentDatabase is not null;

    public string DatabaseLabel => _currentDatabase is null
        ? "Aucune base"
        : System.IO.Path.GetFileName(_currentDatabase.Path);

    public string FormatLabel => _currentDatabase?.Format.ToString() ?? "";

    public string FormatDescription => _currentDatabase switch
    {
        null => "",
        { Format: DatabaseFormat.V1 } => "Réglages numériques complets",
        { Format: DatabaseFormat.V2 } => "Métadonnées uniquement",
        _ => "Format non reconnu",
    };

    [RelayCommand]
    private void ShowLibrary()
    {
        CurrentSection = SectionLibrary;
        CurrentPage = Library;
    }

    [RelayCommand]
    private void ShowSettings()
    {
        CurrentSection = SectionSettings;
        CurrentPage = Settings;
    }

    /// <summary>
    /// Tries the development default (the workspace <c>db</c> folder) first, so a fresh clone
    /// shows real data without any setup. Never throws: a failure only fills the status bar.
    /// </summary>
    public async Task InitializeAsync()
    {
        var candidate = AppPaths.FindDefaultDatabase() ?? Settings.DatabasePath;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            StatusMessage = "Choisissez un fichier Library.db ou Library2.db.";
            ShowSettings();
            return;
        }

        await LoadDatabaseAsync(candidate);
    }

    public async Task LoadDatabaseAsync(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = $"Lecture de {System.IO.Path.GetFileName(path)}…";

        try
        {
            // Reading ~3 000 rows and hashing a 78 MB file must not stall the window.
            var loaded = await Task.Run(() => LoadOffUiThread(path));

            _currentDatabase?.Dispose();
            _currentDatabase = loaded.Database;

            Library.Attach(loaded.Index);
            Settings.Refresh();

            StatusMessage =
                $"{DatabaseLabel} · {loaded.Index.PresetCount:N0} presets · " +
                $"{loaded.Index.ToneModelCount:N0} tone models" +
                (HasKnobSettingsAvailable ? "" : " · réglages non disponibles (V2)");

            NotifyDatabaseChanged();
            ShowLibrary();
        }
        catch (Exception exception)
        {
            StatusMessage = $"Échec : {exception.Message}";
            Settings.ErrorMessage = exception.Message;
            Settings.ClearSummary();
            if (_currentDatabase is null)
                ShowSettings();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static (ToneXDatabase Database, LibraryIndex Index) LoadOffUiThread(string path)
    {
        var database = ToneXDatabase.Open(path);
        try
        {
            var index = new LibraryIndex(database.LoadPresets(), database.LoadToneModels());
            return (database, index);
        }
        catch
        {
            database.Dispose();
            throw;
        }
    }

    private bool HasKnobSettingsAvailable =>
        _currentDatabase is { HasKnobSettings: true };

    private void NotifyDatabaseChanged()
    {
        OnPropertyChanged(nameof(HasDatabase));
        OnPropertyChanged(nameof(DatabaseLabel));
        OnPropertyChanged(nameof(FormatLabel));
        OnPropertyChanged(nameof(FormatDescription));
    }

    partial void OnCurrentSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsLibrarySection));
        OnPropertyChanged(nameof(IsSettingsSection));
    }
}
