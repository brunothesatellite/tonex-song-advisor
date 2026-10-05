using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.App.Localization;
using TonexAdvisor.App.Services;
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
    private bool _isBusy;

    /// <summary>
    /// Dernier compositeur du message de statut : la bascule de langue le rejoue dans la
    /// langue courante (§11.4). Les sites en dur de la v1.2.1 ne se composent pas encore —
    /// leur texte migre en clés à la phase 10.
    /// </summary>
    private Func<string> _statusBuilder = InitialStatus;

    private static string InitialStatus() => "Aucune bibliothèque chargée.";

    [ObservableProperty]
    private string _statusMessage = InitialStatus();

    public MainViewModel(IUserStateStore? stateStore = null)
    {
        Library = new LibraryViewModel(null, stateStore);
        Settings = new SettingsViewModel(this, stateStore);
        _currentPage = Library;

        // Bascule à chaud (§11.4) : le statut affiché se rebâtit dans la langue choisie.
        Localizer.Instance.CultureChanged += RebuildStatus;
    }

    /// <summary>Fixe le statut et mémorise son compositeur pour la prochaine bascule (§11.4).</summary>
    private void SetStatus(Func<string> build)
    {
        _statusBuilder = build;
        StatusMessage = build();
    }

    private void RebuildStatus() => StatusMessage = _statusBuilder();

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
        // Le choix mémorisé passe avant le défaut de développement : si une base a été choisie,
        // c'est elle qui s'ouvre, pas celle du dossier d'exemple.
        var candidate = Settings.EffectiveDatabasePath ?? AppPaths.FindDefaultDatabase();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            SetStatus(static () => "Choisissez un fichier Library.db ou Library2.db.");
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
        SetStatus(() => $"Lecture de {System.IO.Path.GetFileName(path)}…");

        try
        {
            // Reading ~3 000 rows and hashing a 78 MB file must not stall the window.
            var loaded = await Task.Run(() => LoadOffUiThread(path));

            _currentDatabase?.Dispose();
            _currentDatabase = loaded.Database;

            Library.Attach(loaded.Index);
            Settings.Refresh();

            // Le compositeur est conservé : une bascule de langue rejoue ce résumé dans la
            // langue choisie, compteurs et suffixe compris (§11.4).
            SetStatus(() =>
            {
                var resume = Localizer.Instance.Get("Message.Resume.Chargement",
                    DatabaseLabel,
                    Localizer.Instance.Plural(loaded.Index.PresetCount, "Compteur.Presets"),
                    Localizer.Instance.Plural(loaded.Index.ToneModelCount, "Compteur.ToneModels"));
                var suffix = HasKnobSettingsAvailable
                    ? ""
                    : loaded.Joined > 0
                        ? " · " + Localizer.Instance.Get("Message.Resume.Jointe",
                            Localizer.Instance.Plural(loaded.Joined, "Compteur.Presets"))
                        : " · " + Localizer.Instance["Message.Resume.NonDispo"];
                return resume + suffix;
            });

            NotifyDatabaseChanged();
            ShowLibrary();
        }
        catch (Exception exception)
        {
            SetStatus(() => $"Échec : {exception.Message}");
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

    private static (ToneXDatabase Database, LibraryIndex Index, int Joined) LoadOffUiThread(string path)
    {
        var database = ToneXDatabase.Open(path);
        try
        {
            var presets = database.LoadPresets();
            var models = database.LoadToneModels();
            var joined = 0;

            // Génération 2 : les valeurs de potards n'y sont pas — elles sont chiffrées dans les
            // fichiers .txp d'IK. Quand la bibliothèque V1 est à côté, on les reprend là : la
            // jointe se fait en mémoire, aucune base n'est modifiée.
            if (database.Format == DatabaseFormat.V2)
            {
                var companion = SettingsJoin.FindCompanionPath(path);
                if (companion is not null)
                {
                    using var source = ToneXDatabase.Open(companion);

                    var enriched = SettingsJoin.WithSettingsFrom(
                        presets,
                        models,
                        source.LoadPresets(),
                        source.LoadToneModels());

                    joined = enriched.Count(preset => preset.HasKnobSettings)
                             - presets.Count(preset => preset.HasKnobSettings);
                    presets = enriched;

                    source.VerifyUnchanged();
                }
            }

            return (database, new LibraryIndex(presets, models), joined);
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
