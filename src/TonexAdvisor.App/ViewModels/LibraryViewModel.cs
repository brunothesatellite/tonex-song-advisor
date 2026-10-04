using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TonexAdvisor.App.Config;
using TonexAdvisor.App.Services;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.App.ViewModels;

/// <summary>
/// The library browser: a filterable grid of presets and tone models plus a detail panel.
/// </summary>
public partial class LibraryViewModel : ViewModelBase
{
    public const string AnyFilter = "Tous";

    private LibraryIndex? _index;
    private List<PresetRowViewModel> _presetRows = new();
    private List<ToneModelRowViewModel> _toneModelRows = new();

    private ObservableCollection<PresetRowViewModel> _presets = new();
    private ObservableCollection<ToneModelRowViewModel> _toneModels = new();

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedCategory = AnyFilter;

    [ObservableProperty]
    private string _selectedGenre = AnyFilter;

    [ObservableProperty]
    private string _selectedFolder = AnyFilter;

    [ObservableProperty]
    private bool _onlyFavorites;

    [ObservableProperty]
    private PresetRowViewModel? _selectedPreset;

    [ObservableProperty]
    private ToneModelRowViewModel? _selectedToneModel;

    [ObservableProperty]
    private PresetDetailViewModel? _presetDetail;

    [ObservableProperty]
    private ToneModelDetailViewModel? _toneModelDetail;

    [ObservableProperty]
    private string _emptyMessage = "Aucune bibliothèque chargée.";

    [ObservableProperty]
    private string _summary = "";

    [ObservableProperty]
    private int _selectedTabIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDetail))]
    private object? _detailContent;

    public ObservableCollection<string> Categories { get; } = new();

    public ObservableCollection<string> Genres { get; } = new();

    public ObservableCollection<string> Folders { get; } = new();

    /// <summary>
    /// Replaced wholesale on every refresh: notifying 2 500 items individually makes the grid
    /// stutter, while a single reset is imperceptible.
    /// </summary>
    public ObservableCollection<PresetRowViewModel> Presets
    {
        get => _presets;
        private set => SetProperty(ref _presets, value);
    }

    public ObservableCollection<ToneModelRowViewModel> ToneModels
    {
        get => _toneModels;
        private set => SetProperty(ref _toneModels, value);
    }

    public LibraryViewModel(Func<AppConfig>? aiConfig = null, IUserStateStore? stateStore = null)
    {
        _stateStore = stateStore ?? new UserStateStore();
        Advice = new AdviceViewModel(this, aiConfig, _stateStore);
    }

    /// <summary>Where filters are remembered between two runs.</summary>
    private readonly IUserStateStore _stateStore;

    /// <summary>The « Conseils » tab, which ranks this library against a song.</summary>
    public AdviceViewModel Advice { get; }

    public bool HasDatabase => _index is not null;

    public bool HasPresets => Presets.Count > 0;

    public bool HasPresetDetail => PresetDetail is not null;

    public bool HasToneModelDetail => ToneModelDetail is not null;

    /// <summary>True when the right-hand panel has something to render.</summary>
    public bool HasDetail => DetailContent is not null;

    public bool KnobSettingsAvailable => _index is not null && _index.Presets.Any(preset => preset.HasKnobSettings);

    public LibraryIndex? Index => _index;

    /// <summary>Loads the rows and filter vocabularies of a freshly opened library.</summary>
    public void Attach(LibraryIndex index)
    {
        _index = index ?? throw new ArgumentNullException(nameof(index));

        _presetRows = index.Presets
            .Select(preset => new PresetRowViewModel(preset, index.ModelsFor(preset)))
            .ToList();

        _toneModelRows = index.ToneModels
            .Select(model => new ToneModelRowViewModel(model, index.PresetsFor(model.Key).Count))
            .ToList();

        Fill(Categories, _presetRows.Select(row => row.Category));
        Fill(Genres, _presetRows.Select(row => row.Genre));
        Fill(Folders, _presetRows.Select(row => row.Folders)
            .Concat(_toneModelRows.Select(row => row.Folders)));

        SelectedCategory = AnyFilter;
        SelectedGenre = AnyFilter;
        SelectedFolder = AnyFilter;
        SearchText = "";
        OnlyFavorites = false;
        SelectedPreset = null;
        SelectedToneModel = null;
        PresetDetail = null;
        ToneModelDetail = null;
        DetailContent = null;
        SelectedTabIndex = 0;
        Advice.Reset();

        OnPropertyChanged(nameof(HasDatabase));
        OnPropertyChanged(nameof(KnobSettingsAvailable));

        ApplyState();
        Refresh();

        // Start on real data instead of an empty detail panel.
        SelectedPreset = Presets.Count > 0 ? Presets[0] : null;
        SelectedToneModel = ToneModels.Count > 0 ? ToneModels[0] : null;
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = "";
        SelectedCategory = AnyFilter;
        SelectedGenre = AnyFilter;
        SelectedFolder = AnyFilter;
        OnlyFavorites = false;
    }

    [RelayCommand]
    private void ShowAllPresets()
    {
        SelectedCategory = AnyFilter;
        SelectedGenre = AnyFilter;
        SelectedFolder = AnyFilter;
        OnlyFavorites = false;
    }

    partial void OnSearchTextChanged(string value) => FilterChanged();

    partial void OnSelectedCategoryChanged(string value) => FilterChanged();

    partial void OnSelectedGenreChanged(string value) => FilterChanged();

    partial void OnSelectedFolderChanged(string value) => FilterChanged();

    partial void OnOnlyFavoritesChanged(bool value) => FilterChanged();

    /// <summary>Re-filters the grids and remembers the new filters for the next run.</summary>
    private void FilterChanged()
    {
        Refresh();
        SaveState();
    }

    partial void OnSelectedPresetChanged(PresetRowViewModel? value)
    {
        if (value is null || _index is null)
        {
            PresetDetail = null;
        }
        else
        {
            PresetDetail = PresetDetailViewModel.Create(value.Record, value.ToneModels, _index.Ranges);
        }

        UpdateDetailContent();
    }

    partial void OnSelectedToneModelChanged(ToneModelRowViewModel? value)
    {
        if (value is null || _index is null)
        {
            ToneModelDetail = null;
        }
        else
        {
            ToneModelDetail = new ToneModelDetailViewModel(value.Record, _index.PresetsFor(value.Record.Key));
        }

        UpdateDetailContent();
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        UpdateDetailContent();
        SaveState();
    }

    /// <summary>Shows whichever detail matches the active grid tab.</summary>
    private void UpdateDetailContent()
        => DetailContent = SelectedTabIndex == 1 ? (object?)ToneModelDetail : PresetDetail;

    /// <summary>
    /// Leaves the « Conseils » tab and selects a preset the user just asked about. Filters are
    /// reset first: a recommendation the grid is not showing would look broken.
    /// </summary>
    public void OpenPresetByKey(string key)
    {
        ClearFilters();
        SelectedTabIndex = 0;
        SelectedPreset = Presets.FirstOrDefault(row => string.Equals(row.Record.Key, key, StringComparison.Ordinal));
    }

    /// <summary>Same as <see cref="OpenPresetByKey"/>, for the recommended combination.</summary>
    public void OpenToneModelByKey(string key)
    {
        ClearFilters();
        SelectedTabIndex = 1;
        SelectedToneModel = ToneModels.FirstOrDefault(row => string.Equals(row.Record.Key, key, StringComparison.Ordinal));
    }

    /// <summary>Restores the filters of the previous session, when they still make sense.</summary>
    private void ApplyState()
    {
        var state = _stateStore.Load();

        SearchText = state.SearchText;
        SelectedCategory = Categories.Contains(state.Category) ? state.Category : AnyFilter;
        SelectedGenre = Genres.Contains(state.Genre) ? state.Genre : AnyFilter;
        SelectedFolder = Folders.Contains(state.Folder) ? state.Folder : AnyFilter;
        OnlyFavorites = state.OnlyFavorites;
        SelectedTabIndex = state.SelectedTabIndex is >= 0 and <= 2 ? state.SelectedTabIndex : 0;
    }

    /// <summary>
    /// Puts the filters aside for the next run. Written outside the repository, and never
    /// anywhere near the TONEX libraries. Read-modify-write: the settings screen writes the same
    /// file, and neither may erase the fields of the other.
    /// </summary>
    private void SaveState()
    {
        if (_index is null)
            return;

        var state = _stateStore.Load();
        state.SearchText = SearchText;
        state.Category = SelectedCategory == AnyFilter ? "" : SelectedCategory;
        state.Genre = SelectedGenre == AnyFilter ? "" : SelectedGenre;
        state.Folder = SelectedFolder == AnyFilter ? "" : SelectedFolder;
        state.OnlyFavorites = OnlyFavorites;
        state.SelectedTabIndex = SelectedTabIndex;
        _stateStore.Save(state);
    }

    private void Refresh()
    {
        if (_index is null)
        {
            Summary = "";
            return;
        }

        var tokens = Tokenizer.Tokenize(SearchText);
        var category = SelectedCategory;
        var genre = SelectedGenre;
        var folder = SelectedFolder;
        var favoritesOnly = OnlyFavorites;

        var presets = new List<PresetRowViewModel>(_presetRows.Count);
        foreach (var row in _presetRows)
        {
            if (favoritesOnly && !row.Favorite)
                continue;
            if (category != AnyFilter && !string.Equals(row.Category, category, StringComparison.OrdinalIgnoreCase))
                continue;
            if (genre != AnyFilter && !string.Equals(row.Genre, genre, StringComparison.OrdinalIgnoreCase))
                continue;
            if (folder != AnyFilter && !string.Equals(row.Folders, folder, StringComparison.OrdinalIgnoreCase))
                continue;
            if (tokens.Count > 0 && !MatchesTokens(row, tokens))
                continue;

            presets.Add(row);
        }

        var toneModels = new List<ToneModelRowViewModel>(_toneModelRows.Count);
        foreach (var row in _toneModelRows)
        {
            if (favoritesOnly && !row.Favorite)
                continue;
            if (category != AnyFilter && !string.Equals(row.Category, category, StringComparison.OrdinalIgnoreCase))
                continue;
            if (folder != AnyFilter && !string.Equals(row.Folders, folder, StringComparison.OrdinalIgnoreCase))
                continue;
            if (tokens.Count > 0 && !MatchesTokens(row, tokens))
                continue;

            toneModels.Add(row);
        }

        Presets = new ObservableCollection<PresetRowViewModel>(presets);
        ToneModels = new ObservableCollection<ToneModelRowViewModel>(toneModels);

        Summary = $"{presets.Count:N0} preset(s) · {toneModels.Count:N0} tone model(s)" +
                  (HasActiveFilter ? "  —  filtre actif" : "");

        EmptyMessage = _presetRows.Count == 0
            ? "Cette base ne contient aucun preset."
            : "Aucun résultat pour ces filtres.";

        OnPropertyChanged(nameof(HasPresets));
    }

    private bool HasActiveFilter =>
        SearchText.Length > 0
        || SelectedCategory != AnyFilter
        || SelectedGenre != AnyFilter
        || SelectedFolder != AnyFilter
        || OnlyFavorites;

    private static bool MatchesTokens(PresetRowViewModel row, IReadOnlyList<string> tokens)
    {
        var haystack = Tokenizer.Normalize(string.Join(' ',
            row.Name, row.Category, row.Genre, row.Artist, row.Song, row.Amp, row.Cab,
            row.Author, row.Folders));

        foreach (var token in tokens)
        {
            if (!haystack.Contains(token, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static bool MatchesTokens(ToneModelRowViewModel row, IReadOnlyList<string> tokens)
    {
        var haystack = Tokenizer.Normalize(string.Join(' ',
            row.Name, row.Amp, row.Stomp, row.Cab, row.Mics, row.Category,
            row.Folders, row.Author, row.Collection));

        foreach (var token in tokens)
        {
            if (!haystack.Contains(token, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    private static void Fill(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        target.Add(AnyFilter);

        foreach (var value in values
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            target.Add(value);
        }
    }
}
