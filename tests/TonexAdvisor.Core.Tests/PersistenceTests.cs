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
}
