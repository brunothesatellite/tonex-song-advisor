using TonexAdvisor.App.Services;
using TonexAdvisor.App.ViewModels;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Filters must survive a restart — and must go through the injected store, never behind its
/// back to the file of the person running the tests.
/// </summary>
public class PersistenceTests
{
    [LibraryFact]
    public void Filters_TravelFromOneSessionToTheNext_ThroughTheStore()
    {
        var store = new InMemoryUserStateStore();

        var first = new LibraryViewModel(null, store);
        first.Attach(SampleLibraries.Gen1);
        first.SearchText = "metal";
        first.OnlyFavorites = true;
        first.SelectedTabIndex = 2;

        var second = new LibraryViewModel(null, store);
        second.Attach(SampleLibraries.Gen1);

        Assert.Equal("metal", second.SearchText);
        Assert.True(second.OnlyFavorites);
        Assert.Equal(2, second.SelectedTabIndex);
    }

    [LibraryFact]
    public void AFilterThatIsNotInTheLibraryAnymore_IsDroppedNotApplied()
    {
        var store = new InMemoryUserStateStore();
        store.Save(new UserState { Category = "CATÉGORIE-DISPARUE", SelectedTabIndex = 7 });

        var viewModel = new LibraryViewModel(null, store);
        viewModel.Attach(SampleLibraries.Gen1);

        Assert.Equal(LibraryViewModel.AnyFilter, viewModel.SelectedCategory);
        Assert.Equal(0, viewModel.SelectedTabIndex);
    }

    [LibraryFact]
    public void ColumnWidthsChosenByHand_SurviveARestart()
    {
        var store = new InMemoryUserStateStore();

        var first = new LibraryViewModel(null, store);
        first.Attach(SampleLibraries.Gen1);
        first.SavePresetColumnWidths(new Dictionary<string, double> { ["Nom"] = 200, ["Stomp"] = 175 });
        first.SaveToneModelColumnWidths(new Dictionary<string, double> { ["Ampli"] = 180 });

        var second = new LibraryViewModel(null, store);
        second.Attach(SampleLibraries.Gen1);

        Assert.Equal(200, second.PresetColumnWidths["Nom"]);
        Assert.Equal(175, second.PresetColumnWidths["Stomp"]);
        Assert.Equal(180, second.ToneModelColumnWidths["Ampli"]);
    }

    [LibraryFact]
    public void SavingColumnWidths_KeepsTheOtherPreferences()
    {
        var store = new InMemoryUserStateStore();

        var first = new LibraryViewModel(null, store);
        first.Attach(SampleLibraries.Gen1);
        first.SearchText = "metal";
        first.SelectedTabIndex = 1;

        // The screen writes its own file: it must not erase what the browser saved before it.
        first.SavePresetColumnWidths(new Dictionary<string, double> { ["Nom"] = 200 });

        var second = new LibraryViewModel(null, store);
        second.Attach(SampleLibraries.Gen1);

        Assert.Equal("metal", second.SearchText);
        Assert.Equal(1, second.SelectedTabIndex);
        Assert.Equal(200, second.PresetColumnWidths["Nom"]);
    }

    [LibraryFact]
    public void RestoringColumnWidths_NotifiesTheViewOnce()
    {
        var store = new InMemoryUserStateStore();
        store.Save(new UserState { PresetColumnWidths = new Dictionary<string, double> { ["Nom"] = 200 } });

        var viewModel = new LibraryViewModel(null, store);
        var notifications = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(LibraryViewModel.PresetColumnWidths))
                notifications++;
        };

        viewModel.Attach(SampleLibraries.Gen1);

        // The grids listen for this very notification to apply the saved widths.
        Assert.Equal(1, notifications);
        Assert.Equal(200, viewModel.PresetColumnWidths["Nom"]);
    }
}
