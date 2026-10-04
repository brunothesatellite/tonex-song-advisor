using Avalonia;
using System;
using System.Globalization;
using TonexAdvisor.App.Localization;

namespace TonexAdvisor.App;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        FrameCulture();
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    // Cadrage des quatre cultures .NET (§11) depuis la langue d'interface, avant toute
    // construction d'IU. Phase 1 : statu quo français — la détection (fr→FR sinon EN,
    // mémorisée dans state.json) est branchée en phase 9.
    private static void FrameCulture()
        => Localizer.Instance.Culture = CultureInfo.GetCultureInfo("fr-FR");

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
