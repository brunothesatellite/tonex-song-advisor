using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TonexAdvisor.App.ViewModels;
using TonexAdvisor.App.Views;

namespace TonexAdvisor.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var viewModel = new MainViewModel();
            var window = new MainWindow { DataContext = viewModel };

            desktop.MainWindow = window;

            // The window is shown first so a slow library never delays the first paint; the
            // read itself happens off the UI thread inside the view model.
            window.Opened += (_, _) => _ = viewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
