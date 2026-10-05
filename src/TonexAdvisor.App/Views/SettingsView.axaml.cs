using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TonexAdvisor.App.Localization;
using TonexAdvisor.App.ViewModels;

namespace TonexAdvisor.App.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens the OS file picker. Kept in code-behind because only a visual can reach
    /// <see cref="TopLevel.GetTopLevel"/>; the chosen path is handed straight to the view model.
    /// </summary>
    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not SettingsViewModel viewModel)
            return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Localizer.Instance["Reglages.Fichier.Choisir"],
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(Localizer.Instance["Reglages.Fichier.Type"])
                {
                    Patterns = new[] { "*.db" },
                    MimeTypes = new[] { "application/vnd.sqlite3", "application/octet-stream" },
                },
                FilePickerFileTypes.All,
            },
        });

        if (files.Count > 0)
            viewModel.DatabasePath = files[0].Path.LocalPath;
    }
}
