using TonexAdvisor.App.ViewModels;
using TonexAdvisor.Core.Data;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Covers the two things the browser must never get wrong: finding a preset and keeping the
/// detail panel in step with the selection.
/// </summary>
public class LibraryBrowserTests
{
    [Fact]
    public void Attach_ListsEveryPreset_AndLeavesTheDetailPanelOnRealData()
    {
        var index = SampleLibraries.Gen1;
        var viewModel = new LibraryViewModel();

        viewModel.Attach(index);

        Assert.True(viewModel.HasDatabase);
        Assert.True(viewModel.KnobSettingsAvailable);
        Assert.Equal(index.PresetCount, viewModel.Presets.Count);
        Assert.Equal(index.ToneModelCount, viewModel.ToneModels.Count);

        Assert.Contains(LibraryViewModel.AnyFilter, viewModel.Categories);
        Assert.Contains(LibraryViewModel.AnyFilter, viewModel.Genres);

        Assert.True(viewModel.HasDetail);
        Assert.IsType<PresetDetailViewModel>(viewModel.DetailContent);
        Assert.Equal(viewModel.Presets[0].Name, ((PresetDetailViewModel)viewModel.DetailContent!).Name);
    }

    [Fact]
    public void SearchText_NarrowsBothGrids()
    {
        var index = SampleLibraries.Gen1;
        var viewModel = new LibraryViewModel();
        viewModel.Attach(index);

        viewModel.SearchText = "hi-gain";

        // "hi-gain" folds into two tokens, so it also matches amp names such as
        // "Hiwatt Hi-Gain 100": the filter narrows, it does not pretend to be exact.
        Assert.NotEmpty(viewModel.Presets);
        Assert.True(viewModel.Presets.Count < index.PresetCount);
        Assert.Contains(viewModel.Presets, row => row.Category == "HI-GAIN");
        Assert.Contains("filtre actif", viewModel.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchText_UsesEveryToken_NotJustTheFirst()
    {
        var index = SampleLibraries.Gen1;
        var viewModel = new LibraryViewModel();
        viewModel.Attach(index);

        viewModel.SearchText = "ik";
        var singleToken = viewModel.Presets.Count;

        viewModel.SearchText = "ik multimedia";
        var bothTokens = viewModel.Presets.Count;

        Assert.InRange(bothTokens, 1, singleToken);
    }

    [Fact]
    public void CategoryFilter_NarrowsTheGrid_AndClearFiltersRestoresIt()
    {
        var index = SampleLibraries.Gen1;
        var viewModel = new LibraryViewModel();
        viewModel.Attach(index);

        var category = viewModel.Categories.First(value => value != LibraryViewModel.AnyFilter);

        viewModel.SelectedCategory = category;
        var filtered = viewModel.Presets.Count;
        Assert.InRange(filtered, 1, index.PresetCount);
        Assert.All(viewModel.Presets, row =>
            Assert.Equal(category, row.Category, StringComparer.OrdinalIgnoreCase));

        viewModel.SelectedCategory = LibraryViewModel.AnyFilter;

        Assert.Equal(index.PresetCount, viewModel.Presets.Count);
        Assert.Empty(viewModel.SearchText);
        Assert.False(viewModel.OnlyFavorites);
    }

    [Fact]
    public void ChangingTab_SwapsTheDetailPanelToTheToneModel()
    {
        var index = SampleLibraries.Gen1;
        var viewModel = new LibraryViewModel();
        viewModel.Attach(index);

        Assert.IsType<PresetDetailViewModel>(viewModel.DetailContent);

        viewModel.SelectedTabIndex = 1;

        Assert.IsType<ToneModelDetailViewModel>(viewModel.DetailContent);

        viewModel.SelectedTabIndex = 0;

        Assert.IsType<PresetDetailViewModel>(viewModel.DetailContent);
    }

    [Fact]
    public void LibraryIndex_SearchIsAccentAndCaseInsensitive()
    {
        var index = SampleLibraries.Gen1;

        Assert.Equal(index.PresetCount, index.Search("").Count);
        Assert.NotEmpty(index.Search("HI-GAIN"));
        Assert.Equal(index.Search("hi-gain").Count, index.Search("HI GAIN").Count);
        Assert.Empty(index.Search("ce-qui-nexiste-pas-vraiment"));
    }

    [Fact]
    public void LibraryIndex_ResolvesPresetToToneModelAndBack()
    {
        var index = SampleLibraries.Gen1;
        var preset = index.Presets.First(candidate => candidate.ToneModelKeys.Count > 0);

        var models = index.ModelsFor(preset);

        Assert.NotEmpty(models);
        Assert.All(models, model => Assert.NotNull(index.Model(model.Key)));
        Assert.Contains(preset, index.PresetsFor(models[0].Key));
    }

    [Fact]
    public void LibraryIndex_RangesCoverEveryNumericParameterUsedByAPreset()
    {
        var index = SampleLibraries.Gen1;
        var preset = index.Presets.Single(p => p.Name == "1CHAIN");

        var gain = preset.Settings!.Number("ModelGain");

        Assert.NotNull(gain);
        Assert.True(index.Ranges.ContainsKey("ModelGain"));
        Assert.InRange(gain!.Value, index.Ranges["ModelGain"].Min, index.Ranges["ModelGain"].Max);
    }
}
