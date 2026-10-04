using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.App.Config;
using TonexAdvisor.App.Services;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.App.ViewModels;

/// <summary>Database selection plus a read-only summary of what was opened, and the AI settings.</summary>
public partial class SettingsViewModel : ViewModelBase
{
    private readonly IDatabaseHost _host;
    private readonly IUserStateStore _stateStore;
    private readonly AppConfig _config;

    /// <summary>La base choisie dans la liste TONEX, mémorisée par son chemin.</summary>
    private string _selectedTonexPath = "";

    /// <summary>Faux pendant la construction : rien ne doit se charger tout seul à ce moment-là.</summary>
    private bool _ready;

    /// <summary>Le dossier TONEX à explorer ; le dossier officiel par défaut.</summary>
    private readonly string? _tonexFolder;

    private readonly Func<string, Task<IReadOnlyList<string>>>? _modelFetcher;

    /// <summary>
    /// How the configuration is written. The default writes the real file; tests inject a no-op,
    /// because a test must never touch the configuration of the person running it.
    /// </summary>
    private readonly Action<AppConfig> _saveConfig;

    [ObservableProperty]
    private string _databasePath = "";

    // ── Choix de la base : chemin manuel, ou dossier TONEX ─────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManualEnabled), nameof(IsTonexEnabled))]
    private bool _useTonexLibrary;

    [ObservableProperty]
    private LibraryFile? _selectedTonexLibrary;

    /// <summary>Les bases trouvées à la racine du dossier TONEX.</summary>
    public ObservableCollection<LibraryFile> TonexLibraries { get; } = new();

    /// <summary>Le dossier officiel, trouvé par le « Documents » connu de Windows.</summary>
    public string TonexFolder => _tonexFolder ?? TonexLibraryFolder.DefaultPath;

    /// <summary>La case n'est proposée que s'il y a quelque chose à choisir.</summary>
    public bool CanUseTonex => TonexLibraries.Count > 0;

    public bool IsManualEnabled => !UseTonexLibrary;

    public bool IsTonexEnabled => UseTonexLibrary;

    public string TonexFolderHint => TonexLibraries.Count == 0
        ? $"Aucune base trouvée dans {TonexFolder}."
        : $"{TonexLibraries.Count} base(s) trouvée(s) dans {TonexFolder} — si la base est en génération 2 et qu'une V1 est à côté, ses réglages sont joints automatiquement.";

    /// <summary>La base à ouvrir au démarrage : le choix mémorisé, s'il y en a un.</summary>
    public string? EffectiveDatabasePath =>
        UseTonexLibrary
            ? SelectedTonexLibrary?.Path
            : string.IsNullOrWhiteSpace(DatabasePath) ? null : DatabasePath.Trim();

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

    // ── IA (OpenCode Go) ────────────────────────────────────────────────────

    [ObservableProperty]
    private string _apiKey = "";

    [ObservableProperty]
    private OpenCodeModel? _selectedModel;

    /// <summary>
    /// The OpenCode models offered as voices. Ticked = one voice each, in the order of the list;
    /// the arbitre is a different job and lives in <see cref="SelectedModel"/>.
    /// </summary>
    public ObservableCollection<VoiceChoiceViewModel> VoiceChoices { get; } = new();

    /// <summary>What happened to the ticked voices at the last catalogue update.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVoiceModelsStatus))]
    private string _voiceModelsStatus = "";

    public bool HasVoiceModelsStatus => VoiceModelsStatus.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAiStatus))]
    private string _aiStatusMessage = "";

    [ObservableProperty]
    private bool _isTestingAi;

    /// <summary>
    /// Tous les modèles accessibles avec la clé, gratuits comme payants — la liste vient de
    /// l'API, pas d'une liste en dur qui deviendrait fausse le jour où un modèle disparaît.
    /// </summary>
    public ObservableCollection<OpenCodeModel> AvailableModels { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRefreshModels))]
    private bool _isRefreshingModels;

    public bool CanRefreshModels => !IsRefreshingModels;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModelStatus))]
    private string _modelStatus = "";

    public bool HasModelStatus => ModelStatus.Length > 0;

    // ── Avis croisés : voix challengeres (Gemini, Mistral, Groq) ───────────

    [ObservableProperty]
    private string _geminiKey = "";

    [ObservableProperty]
    private string _mistralKey = "";

    [ObservableProperty]
    private string _groqKey = "";

    /// <summary>Modèle explicite ; vide = le modèle gratuit par défaut du catalogue.</summary>
    [ObservableProperty]
    private string _geminiModel = "";

    [ObservableProperty]
    private string _mistralModel = "";

    [ObservableProperty]
    private string _groqModel = "";

    /// <summary>Les défauts viennent du catalogue : l'IU ne les duplique jamais.</summary>
    public string GeminiDefaultModel => AiProviders.Find("gemini")!.DefaultModel;

    public string MistralDefaultModel => AiProviders.Find("mistral")!.DefaultModel;

    public string GroqDefaultModel => AiProviders.Find("groq")!.DefaultModel;

    public string GeminiModelHint => $"modèle (défaut : {GeminiDefaultModel})";

    public string MistralModelHint => $"modèle (défaut : {MistralDefaultModel})";

    public string GroqModelHint => $"modèle (défaut : {GroqDefaultModel})";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProviderStatus))]
    private string _providerStatus = "";

    /// <summary>True dès qu'un message de statut s'affiche pour les voix challengeres.</summary>
    public bool HasProviderStatus => ProviderStatus.Length > 0;

    /// <summary>Où se trouve le fichier de configuration (hors dépôt Git).</summary>
    public string ConfigPath => AppConfig.DefaultPath;

    /// <summary>True dès qu'un message de statut IA s'affiche.</summary>
    public bool HasAiStatus => AiStatusMessage.Length > 0;

    public SettingsViewModel(
        IDatabaseHost host,
        IUserStateStore? stateStore = null,
        string? tonexFolder = null,
        Func<string, Task<IReadOnlyList<string>>>? modelFetcher = null,
        AppConfig? config = null,
        Action<AppConfig>? saveConfig = null)
    {
        _host = host ?? throw new ArgumentNullException(nameof(host));
        _stateStore = stateStore ?? new UserStateStore();
        _tonexFolder = tonexFolder;
        _modelFetcher = modelFetcher;
        _saveConfig = saveConfig ?? (saved => saved.Save());
        _config = config ?? AppConfig.Load();

        var state = _stateStore.Load();
        _databasePath = state.DatabasePath;
        _useTonexLibrary = state.UseTonexLibrary;
        _selectedTonexPath = state.TonexDatabasePath;

        _apiKey = _config.ApiKey;
        _geminiKey = _config.CredentialFor("gemini").ApiKey;
        _mistralKey = _config.CredentialFor("mistral").ApiKey;
        _groqKey = _config.CredentialFor("groq").ApiKey;
        _geminiModel = _config.CredentialFor("gemini").Model;
        _mistralModel = _config.CredentialFor("mistral").Model;
        _groqModel = _config.CredentialFor("groq").Model;

        // Les voix gratuites : une clé renseignée ne suffit pas, il faut que la voix soit active.
        _geminiActive = !state.DisabledVoices.Contains("gemini");
        _mistralActive = !state.DisabledVoices.Contains("mistral");
        _groqActive = !state.DisabledVoices.Contains("groq");

        // Le catalogue de modèles : celui en cache, ou les deux connus par défaut.
        var catalog = _config.ModelCatalog.Count > 0
            ? _config.ModelCatalog.Select(OpenCodeModels.FromId).ToList()
            : OpenCodeModels.Defaults.ToList();

        foreach (var model in catalog)
            AvailableModels.Add(model);

        _selectedModel = AvailableModels.FirstOrDefault(model =>
                             string.Equals(model.Id, _config.Model, StringComparison.OrdinalIgnoreCase))
                         ?? OpenCodeModels.Fallback(AvailableModels);

        // Les voix sont construites tout de suite : une liste vide tant qu'on n'a pas cliqué
        // « Mise à jour » n'est pas une liste.
        RebuildVoiceChoices();

        RefreshTonexLibraries();
        _ready = true;
    }

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

    // ── IA : enregistrement et test de la clé ──────────────────────────────

    /// <summary>Copie les champs d'interface vers le fichier de configuration.</summary>
    private AppConfig PersistAi()
    {
        _config.ApiKey = ApiKey.Trim();
        _config.Model = SelectedModel?.Id ?? OpenCodeModels.Fallback(AvailableModels).Id;
        _config.Endpoint = OpenCodeModels.EndPoint;
        _config.ModelCatalog = AvailableModels.Select(model => model.Id).ToList();
        _config.VoiceModels = VoiceChoices
            .Where(choice => choice.IsChecked)
            .Select(choice => choice.Model.Id)
            .ToList();
        PersistProvider("gemini", GeminiKey, GeminiModel);
        PersistProvider("mistral", MistralKey, MistralModel);
        PersistProvider("groq", GroqKey, GroqModel);
        _saveConfig(_config);
        return _config;
    }

    /// <summary>
    /// Enregistre la clé et le modèle d'une voix challenger ; le modèle reste vide tant que
    /// l'utilisateur n'a pas besoin de forcer le choix du catalogue.
    /// </summary>
    private void PersistProvider(string providerId, string apiKey, string model)
    {
        var credential = _config.CredentialFor(providerId);
        credential.ApiKey = apiKey.Trim();
        credential.Model = model.Trim();
        _config.Providers[providerId] = credential;
    }

    [RelayCommand]
    private void SaveAi()
    {
        var keyChanged = !string.Equals(ApiKey.Trim(), _config.ApiKey, StringComparison.Ordinal);

        PersistAi();
        AiStatusMessage = $"Enregistré dans {AppConfig.DefaultPath}";

        // Une clé qui change ouvre d'autres modèles : on relit la liste.
        if (keyChanged)
            _ = RefreshModelsAsync();
    }

    /// <summary>Réduit une réponse IA à une ligne courte, pour la barre de statut.</summary>
    private static string Flatten(string value, int max)
    {
        var words = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var single = string.Join(' ', words);
        return single.Length <= max ? single : single[..max].TrimEnd() + "…";
    }

    /// <summary>Envoie une requête réelle pour vérifier la clé et le modèle choisi.</summary>
    [RelayCommand]
    private async Task TestAiAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            AiStatusMessage = "Renseignez d'abord la clé API OpenCode.";
            return;
        }

        var config = PersistAi();
        var label = config.Model;
        IsTestingAi = true;
        AiStatusMessage = $"Test avec {label}…";

        try
        {
            var client = new OpenCodeClient(config);
            var answer = await client.PingAsync(config.Model).ConfigureAwait(true);
            AiStatusMessage = answer.Trim().Length == 0
                ? $"Connecté avec {label}, mais la réponse est vide."
                : $"Connexion OK avec {label} : « {Flatten(answer, 120)} »";
        }
        catch (Exception exception)
        {
            AiStatusMessage = exception.Message;
        }
        finally
        {
            IsTestingAi = false;
        }
    }

    /// <summary>
    /// Teste une par une les voix challengeres renseignées : une clé invalide est signalée sans
    /// empêcher de tester les autres.
    /// </summary>
    [RelayCommand]
    private async Task TestProvidersAsync()
    {
        var config = PersistAi();

        if (AiProviders.Challengers.All(provider =>
                string.IsNullOrWhiteSpace(config.CredentialFor(provider.Id).ApiKey)))
        {
            ProviderStatus = "Renseigne au moins une clé challenger (Gemini, Mistral ou Groq).";
            return;
        }

        IsTestingAi = true;
        try
        {
            var results = new List<string>();

            foreach (var provider in AiProviders.Challengers)
            {
                var credential = config.CredentialFor(provider.Id);
                if (string.IsNullOrWhiteSpace(credential.ApiKey))
                {
                    results.Add($"{provider.Label} : non renseigné");
                    continue;
                }

                var model = string.IsNullOrWhiteSpace(credential.Model)
                    ? provider.DefaultModel
                    : credential.Model;

                try
                {
                    var client = new OpenAiCompatClient(provider.Label, provider.EndPoint, credential.ApiKey);
                    var answer = await client
                        .AskStreamAsync(model, "Réponds uniquement par le mot OK.", 800, _ => { })
                        .ConfigureAwait(true);

                    results.Add(answer.Trim().Length == 0
                        ? $"{provider.Label} : connecté, réponse vide"
                        : $"{provider.Label} : OK « {Flatten(answer, 60)} »");
                }
                catch (Exception exception)
                {
                    var detail = $"{provider.Label} : {Flatten(exception.Message, 260)}";

                    // Un modèle retiré est la panne la plus courante : on montre ceux qui sont
                    // réellement ouverts à cette clé, sans que l'utilisateur ait à deviner.
                    var available = await TryListModelsAsync(provider, credential).ConfigureAwait(true);
                    if (available.Length > 0)
                        detail += $"  ·  modèles : {available}";

                    results.Add(detail);
                }
            }

            ProviderStatus = string.Join("  ·  ", results);
        }
        finally
        {
            IsTestingAi = false;
        }
    }

    /// <summary>
    /// Liste les modèles ouverts à une clé, pour dépanner un modèle retiré. Un échec ici ne
    /// change rien au diagnostic principal.
    /// </summary>
    private static async Task<string> TryListModelsAsync(AiProvider provider, ProviderCredential credential)
    {
        try
        {
            var client = new OpenAiCompatClient(provider.Label, provider.EndPoint, credential.ApiKey);
            var models = await client.ListModelsAsync().ConfigureAwait(true);

            return models.Count == 0
                ? ""
                : string.Join(", ", models.Take(20));
        }
        catch (Exception)
        {
            return "";
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

        // En mode « dossier TONEX », le champ du choix 1 garde le chemin de l'utilisateur : c'est
        // lui qui reprend quand on décoche la case. Recopier la base ouverte ici l'écraserait.
        if (!UseTonexLibrary)
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

        SaveState();
    }

    // ── Choix de la base ───────────────────────────────────────────────────

    partial void OnUseTonexLibraryChanged(bool value)
    {
        SaveState();

        if (!_ready)
            return;

        if (value)
        {
            RefreshTonexLibraries();
            if (SelectedTonexLibrary is not null)
                _ = LoadFromAsync(SelectedTonexLibrary.Path);
            return;
        }

        // Retour au choix 1 : la base du champ de chemin est rechargée, sans clic supplémentaire.
        if (!string.IsNullOrWhiteSpace(DatabasePath))
            _ = LoadFromAsync(DatabasePath.Trim());
    }

    partial void OnSelectedTonexLibraryChanged(LibraryFile? value)
    {
        SaveState();

        if (_ready && value is not null && UseTonexLibrary)
            _ = LoadFromAsync(value.Path);
    }

    /// <summary>Les bases de la racine du dossier TONEX, la plus récente d'abord.</summary>
    private void RefreshTonexLibraries()
    {
        TonexLibraries.Clear();

        foreach (var library in TonexLibraryFolder.FindDatabases(TonexFolder))
            TonexLibraries.Add(library);

        SelectedTonexLibrary = TonexLibraries.FirstOrDefault(library =>
                                 string.Equals(library.Path, _selectedTonexPath, StringComparison.OrdinalIgnoreCase))
                               ?? TonexLibraries.FirstOrDefault();

        OnPropertyChanged(nameof(CanUseTonex));
        OnPropertyChanged(nameof(TonexFolderHint));

        // Rien à choisir : la case n'a pas de sens, on reste sur le chemin manuel.
        if (TonexLibraries.Count == 0 && UseTonexLibrary)
            UseTonexLibrary = false;
    }

    /// <summary>Charge une base choisie ailleurs que par le champ de chemin.</summary>
    private async Task LoadFromAsync(string path)
    {
        ErrorMessage = "";

        try
        {
            await _host.LoadDatabaseAsync(path).ConfigureAwait(true);
            Refresh();
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
            ClearSummary();
        }
    }

    /// <summary>Mémorise le choix de base. Lecture-modification-écriture : l'écran de la bibliothèque
    /// écrit le même fichier, et aucun des deux ne doit effacer les champs de l'autre.
    /// </summary>
    private void SaveState()
    {
        var state = _stateStore.Load();
        state.DatabasePath = DatabasePath;
        state.UseTonexLibrary = UseTonexLibrary;
        state.TonexDatabasePath = SelectedTonexLibrary?.Path ?? _selectedTonexPath;
        state.DisabledVoices = DisabledVoicesFromToggles();
        _stateStore.Save(state);
    }

    // ── Les voix : une clé renseignée ne suffit pas, il faut l'activer ─────

    [ObservableProperty]
    private bool _geminiActive = true;

    [ObservableProperty]
    private bool _mistralActive = true;

    [ObservableProperty]
    private bool _groqActive = true;

    partial void OnGeminiActiveChanged(bool value) => SaveVoices();


    partial void OnMistralActiveChanged(bool value) => SaveVoices();

    partial void OnGroqActiveChanged(bool value) => SaveVoices();

    /// <summary>
    /// Les voix qui participent au conseil : les modèles OpenCode cochés, et les voix gratuites
    /// laissées allumées.
    /// </summary>
    /// <summary>
    /// One ticked box per model of the catalogue. A ticked voice that has disappeared is named
    /// in the message — never silently replaced.
    /// </summary>
    private void RebuildVoiceChoices()
    {
        var wanted = _config.VoiceModels
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        VoiceChoices.Clear();
        foreach (var model in AvailableModels)
            VoiceChoices.Add(new VoiceChoiceViewModel(model, wanted.Contains(model.Id), SaveVoices));

        var gone = wanted
            .Where(id => AvailableModels.All(model =>
                !string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        VoiceModelsStatus = gone.Count == 0
            ? ""
            : $"{gone.Count} voix cochée(s) ne sont plus disponibles : " +
              $"{string.Join(", ", gone)}. Elles ont été décochées — rien n'a été coché à leur place.";
    }

    private void SaveVoices()
    {
        _config.VoiceModels = VoiceChoices
            .Where(choice => choice.IsChecked)
            .Select(choice => choice.Model.Id)
            .ToList();
        _saveConfig(_config);

        var state = _stateStore.Load();
        state.DisabledVoices = DisabledVoicesFromToggles();
        _stateStore.Save(state);
    }

    /// <summary>Les identifiants des voix gratuites que l'utilisateur a éteintes.</summary>
    private List<string> DisabledVoicesFromToggles()
    {
        var disabled = new List<string>();

        if (!GeminiActive)
            disabled.Add("gemini");
        if (!MistralActive)
            disabled.Add("mistral");
        if (!GroqActive)
            disabled.Add("groq");

        return disabled;
    }

    // ── Les modèles OpenCode : la liste vient de l'API ─────────────────────

    /// <summary>
    /// Demande la liste des modèles à l'API. Déclenché à l'enregistrement quand la clé change et
    /// au bouton « Mise à jour » : un appel par frappe dans le champ de clé serait absurde.
    /// </summary>
    [RelayCommand]
    private async Task RefreshModelsAsync()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            ModelStatus = "Renseignez d'abord la clé API OpenCode.";
            return;
        }

        IsRefreshingModels = true;
        ModelStatus = "Lecture des modèles…";

        try
        {
            var ids = _modelFetcher is not null
                ? await _modelFetcher(ApiKey.Trim()).ConfigureAwait(true)
                : await new OpenCodeClient(new AppConfig
                    {
                        ApiKey = ApiKey.Trim(),
                        Model = SelectedModel?.Id ?? "",
                        Endpoint = OpenCodeModels.EndPoint,
                    })
                    .ListModelsAsync()
                    .ConfigureAwait(true);

            ApplyModelCatalog(ids);
        }
        catch (Exception exception)
        {
            ModelStatus = exception.Message;
        }
        finally
        {
            IsRefreshingModels = false;
        }
    }

    /// <summary>
    /// Replace the catalogue. When the chosen model has disappeared, say so and fall back on the
    /// first free model — LongCat if it is still there.
    /// </summary>
    private void ApplyModelCatalog(IReadOnlyList<string> ids)
    {
        AvailableModels.Clear();

        foreach (var id in ids.OrderBy(id => id, StringComparer.OrdinalIgnoreCase))
            AvailableModels.Add(OpenCodeModels.FromId(id));

        if (AvailableModels.Count == 0)
        {
            foreach (var model in OpenCodeModels.Defaults)
                AvailableModels.Add(model);
        }

        // Les voix : une case par modèle, dans l'ordre de la liste du catalogue.
        RebuildVoiceChoices();

        var selected = AvailableModels.FirstOrDefault(model =>
            string.Equals(model.Id, SelectedModel?.Id, StringComparison.OrdinalIgnoreCase));

        if (selected is null)
        {
            var fallback = OpenCodeModels.Fallback(AvailableModels);
            ModelStatus = SelectedModel is null
                ? $"{AvailableModels.Count} modèles trouvés, dont {AvailableModels.Count(model => model.IsFree)} gratuits."
                : $"Le modèle « {SelectedModel.Id} » n'est plus disponible : « {fallback.Id} » est sélectionné à sa place.";
            SelectedModel = fallback;
        }
        else
        {
            ModelStatus = $"{AvailableModels.Count} modèles trouvés, dont {AvailableModels.Count(model => model.IsFree)} gratuits.";
            SelectedModel = selected;
        }

        _config.ModelCatalog = AvailableModels.Select(model => model.Id).ToList();
        _saveConfig(_config);
    }
}
