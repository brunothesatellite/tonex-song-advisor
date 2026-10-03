using TonexAdvisor.App.ViewModels;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// The « Conseils » tab is the only screen that has to go from a sentence to a selected row
/// without the user touching the grids.
/// </summary>
public class AdviceViewTests
{
    [Fact]
    public void Advise_FillsTheTab_ThenOpensTheRecommendedPreset()
    {
        var viewModel = new LibraryViewModel();
        viewModel.Attach(SampleLibraries.Gen1);

        viewModel.Advice.Style = "metal";
        viewModel.Advice.AdviseCommand.Execute(null);

        Assert.False(viewModel.Advice.HasError);
        Assert.True(viewModel.Advice.HasPresets);
        Assert.True(viewModel.Advice.HasCombination);
        Assert.InRange(viewModel.Advice.Presets.Count, 1, 3);
        Assert.False(string.IsNullOrWhiteSpace(viewModel.Advice.Hint));

        var recommended = viewModel.Advice.Presets[0];

        // The user is on the advice tab when they click "Ouvrir".
        viewModel.SelectedTabIndex = 2;
        recommended.OpenCommand.Execute(null);

        Assert.Equal(0, viewModel.SelectedTabIndex);
        Assert.NotNull(viewModel.SelectedPreset);
        Assert.Equal(recommended.Key, viewModel.SelectedPreset!.Record.Key);
        Assert.IsType<PresetDetailViewModel>(viewModel.DetailContent);
        Assert.Empty(viewModel.SearchText);
        Assert.Equal(LibraryViewModel.AnyFilter, viewModel.SelectedCategory);
    }

    [Fact]
    public void Advise_OpensTheRecommendedToneModelOnItsTab()
    {
        var viewModel = new LibraryViewModel();
        viewModel.Attach(SampleLibraries.Gen1);

        viewModel.Advice.Style = "metal";
        viewModel.Advice.AdviseCommand.Execute(null);

        var combination = viewModel.Advice.Combination;
        Assert.NotNull(combination);

        viewModel.SelectedTabIndex = 2;
        combination!.OpenCommand.Execute(null);

        Assert.Equal(1, viewModel.SelectedTabIndex);
        Assert.NotNull(viewModel.SelectedToneModel);
        Assert.Equal(combination.Key, viewModel.SelectedToneModel!.Record.Key);
        Assert.IsType<ToneModelDetailViewModel>(viewModel.DetailContent);
    }

    [Fact]
    public void Advise_WithoutAQuery_ReportsInsteadOfGuessing()
    {
        var viewModel = new LibraryViewModel();
        viewModel.Attach(SampleLibraries.Gen1);

        viewModel.Advice.AdviseCommand.Execute(null);

        Assert.True(viewModel.Advice.HasError);
        Assert.False(viewModel.Advice.HasPresets);
        Assert.False(viewModel.Advice.HasCombination);
    }

    [Fact]
    public void Advise_WithoutALibrary_ReportsInsteadOfCrashing()
    {
        var viewModel = new LibraryViewModel();

        viewModel.Advice.Style = "metal";
        viewModel.Advice.AdviseCommand.Execute(null);

        Assert.True(viewModel.Advice.HasError);
        Assert.Contains("bibliothèque", viewModel.Advice.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Attach_ClearsTheAdviceOfThePreviousLibrary()
    {
        var viewModel = new LibraryViewModel();
        viewModel.Attach(SampleLibraries.Gen1);

        viewModel.Advice.Style = "metal";
        viewModel.Advice.AdviseCommand.Execute(null);
        Assert.True(viewModel.Advice.HasPresets);

        viewModel.Attach(SampleLibraries.Gen1);

        Assert.False(viewModel.Advice.HasRun);
        Assert.False(viewModel.Advice.HasPresets);
        Assert.False(viewModel.Advice.HasCombination);
        Assert.False(viewModel.Advice.HasError);
    }
}
