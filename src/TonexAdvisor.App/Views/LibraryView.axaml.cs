using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TonexAdvisor.App.ViewModels;

namespace TonexAdvisor.App.Views;

public partial class LibraryView : UserControl
{
    private LibraryViewModel? _viewModel;

    public LibraryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    /// <summary>
    /// The indeterminate animation of a progress bar stops when its tab is unloaded and does not
    /// always come back: toggling the property restarts it. The elapsed seconds around it are the
    /// real sign of life anyway.
    /// </summary>
    private void RestartSpinners()
    {
        foreach (var bar in this.GetVisualDescendants().OfType<ProgressBar>())
        {
            bar.IsIndeterminate = false;
            bar.IsIndeterminate = true;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as LibraryViewModel;

        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>
    /// A recommendation the user opens must be <b>visible</b>: selecting the row is not enough,
    /// the grid has to scroll to it. Posted to the dispatcher so the selection is in place before
    /// the grid is asked to bring it into view.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
            return;

        if (e.PropertyName == nameof(LibraryViewModel.SelectedPreset)
            && _viewModel.SelectedPreset is { } preset)
        {
            Dispatcher.UIThread.Post(() =>
                PresetsGrid.ScrollIntoView(preset, PresetsGrid.Columns[0]));
        }
        else if (e.PropertyName == nameof(LibraryViewModel.SelectedToneModel)
                 && _viewModel.SelectedToneModel is { } model)
        {
            Dispatcher.UIThread.Post(() =>
                ToneModelsGrid.ScrollIntoView(model, ToneModelsGrid.Columns[0]));
        }
        else if (e.PropertyName == nameof(LibraryViewModel.SelectedTabIndex)
                 && _viewModel.SelectedTabIndex == 2)
        {
            // Retour sur « Conseils » : les animations des spinners reprennent.
            RestartSpinners();
        }
    }
}
