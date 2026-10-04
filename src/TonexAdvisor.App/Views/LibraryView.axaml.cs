using System.Linq;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
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

        // Le poignée de redimensionnement de l'en-tête consomme l'événement : on l'écoute quand
        // même, sinon la fin du glissé ne serait jamais vue.
        PresetsGrid.AddHandler(InputElement.PointerReleasedEvent, OnGridPointerReleased,
            handledEventsToo: true);
        ToneModelsGrid.AddHandler(InputElement.PointerReleasedEvent, OnGridPointerReleased,
            handledEventsToo: true);
        PresetsGrid.AddHandler(InputElement.PointerPressedEvent, OnGridPointerPressed,
            handledEventsToo: true);
        ToneModelsGrid.AddHandler(InputElement.PointerPressedEvent, OnGridPointerPressed,
            handledEventsToo: true);

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

        ApplySavedColumnWidths();
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
        else if (e.PropertyName == nameof(LibraryViewModel.PresetColumnWidths))
        {
            ApplyColumnWidths(PresetsGrid, _viewModel.PresetColumnWidths);
        }
        else if (e.PropertyName == nameof(LibraryViewModel.ToneModelColumnWidths))
        {
            ApplyColumnWidths(ToneModelsGrid, _viewModel.ToneModelColumnWidths);
        }
    }

    /// <summary>
    /// Gives each column the width the user gave it, in pixels. A column in pixels is out of the
    /// shared space for good: the window can no longer take its width back, and the columns nobody
    /// touched keep adapting through their <c>*</c>.
    /// </summary>
    private void ApplySavedColumnWidths()
    {
        if (_viewModel is null)
            return;

        ApplyColumnWidths(PresetsGrid, _viewModel.PresetColumnWidths);
        ApplyColumnWidths(ToneModelsGrid, _viewModel.ToneModelColumnWidths);
    }

    private static void ApplyColumnWidths(DataGrid grid, IReadOnlyDictionary<string, double> widths)
    {
        foreach (var (header, pixels) in widths)
        {
            // Below any sensible minimum: not a width a hand could have chosen.
            if (pixels < 40)
                continue;

            var column = grid.Columns.FirstOrDefault(candidate =>
                string.Equals(candidate.Header?.ToString(), header, StringComparison.Ordinal));

            if (column is not null)
                column.Width = new DataGridLength(pixels, DataGridLengthUnitType.Pixel);
        }
    }

    private DataGrid? _pressedGrid;

    private DataGridColumn? _pressedColumn;

    private double[] _widthsBeforePress = [];

    /// <summary>
    /// Snapshots the widths and remembers which border was grabbed, so the release can tell a drag
    /// from a plain click - and know which column the drag resized.
    /// </summary>
    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not DataGrid grid)
            return;

        _pressedGrid = grid;
        _pressedColumn = ColumnAtBorder(grid, e.GetPosition(grid).X);
        _widthsBeforePress = grid.Columns.Select(column => column.Width.Value).ToArray();
    }

    /// <summary>
    /// The column whose right border sits under the pointer, give or take the width of the grip.
    /// </summary>
    private static DataGridColumn? ColumnAtBorder(DataGrid grid, double x)
    {
        var edge = 0.0;

        foreach (var column in grid.Columns)
        {
            edge += column.ActualWidth;
            if (Math.Abs(x - edge) <= 10)
                return column;
        }

        return null;
    }

    /// <summary>
    /// The end of a drag: the column just left the proportional units, so the width it was given
    /// is memorised. Nothing moved if nothing changed - a plain click must not rewrite the file.
    /// </summary>
    private void OnGridPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_viewModel is null || sender is not DataGrid grid || !ReferenceEquals(grid, _pressedGrid))
        {
            _pressedGrid = null;
            _pressedColumn = null;
            _widthsBeforePress = [];
            return;
        }

        var before = _widthsBeforePress;
        _pressedGrid = null;
        _widthsBeforePress = [];

        if (before.Length != grid.Columns.Count)
            return;

        var moved = false;
        for (var index = 0; index < before.Length && !moved; index++)
            moved = Math.Abs(grid.Columns[index].Width.Value - before[index]) > 0.5;

        var dragged = _pressedColumn;
        _pressedColumn = null;

        if (!moved || dragged is null)
            return;

        // A drag leaves the star world: the column keeps the width it has in pixels, and never
        // hands it back when the window changes size.
        if (!dragged.Width.IsAbsolute)
            dragged.Width = new DataGridLength(dragged.ActualWidth, DataGridLengthUnitType.Pixel);

        var widths = new Dictionary<string, double>();
        foreach (var column in grid.Columns)
        {
            var header = column.Header?.ToString();
            if (string.IsNullOrEmpty(header))
                continue;

            // Every column already frozen stays frozen; the dragged one joins them at the width
            // it just reached. The others keep their '*' and keep adapting.
            if (column.Width.IsAbsolute || ReferenceEquals(column, dragged))
                widths[header] = column.ActualWidth;
        }

        if (ReferenceEquals(grid, PresetsGrid))
            _viewModel.SavePresetColumnWidths(widths);
        else
            _viewModel.SaveToneModelColumnWidths(widths);
    }
}
