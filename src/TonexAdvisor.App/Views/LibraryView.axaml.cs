using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TonexAdvisor.App.ViewModels;

namespace TonexAdvisor.App.Views;

public partial class LibraryView : UserControl
{
    private LibraryViewModel? _viewModel;

    private readonly DispatcherTimer _spinTimer = new DispatcherTimer
    {
        Interval = TimeSpan.FromMilliseconds(80),
    };

    private double _spinAngle;

    public LibraryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        _spinTimer.Tick += OnSpinTick;
        _spinTimer.Start();
    }

    /// <summary>
    /// Rotates the spinners by hand. The indeterminate animation of a progress bar stops when its
    /// tab is unloaded and never comes back; an angle driven by a timer cannot stall.
    /// </summary>
    private void OnSpinTick(object? sender, EventArgs e)
    {
        if (_viewModel?.Advice.IsAiBusy != true)
            return;

        _spinAngle = (_spinAngle + 15) % 360;

        foreach (var spinner in this.GetVisualDescendants().OfType<Ellipse>()
                     .Where(ellipse => Equals(ellipse.Tag, "spinner") && ellipse.IsVisible))
        {
            if (spinner.RenderTransform is not RotateTransform rotate)
            {
                rotate = new RotateTransform();
                spinner.RenderTransform = rotate;
            }

            rotate.Angle = _spinAngle;
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

    }
}
