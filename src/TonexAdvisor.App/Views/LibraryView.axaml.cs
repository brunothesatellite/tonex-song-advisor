using Avalonia.Controls;
using Avalonia.Threading;
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
    }
}
