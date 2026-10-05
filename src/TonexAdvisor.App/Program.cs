using Avalonia;
using System;
using System.Globalization;
using System.IO;
using TonexAdvisor.App.Localization;
using TonexAdvisor.App.Services;

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

    // Détection puis cadrage (§11) : le choix mémorisé d'abord, la machine à défaut (« fr » →
    // français, tout le reste → anglais), l'écriture ensuite pour que le prochain lancement
    // parte du choix explicite. Avant toute construction d'IU, et les quatre cultures .NET
    // avec — un thread arrière-plan (lecture BDD, appel IA) doit formater pareil (§11).
    private static void FrameCulture()
    {
        var state = UserState.Load();
        var language = UiLanguages.Resolve(state.UiLanguage, CultureInfo.CurrentUICulture);

        if (!string.Equals(state.UiLanguage, language, StringComparison.Ordinal))
        {
            state.UiLanguage = language;

            try
            {
                state.Save();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Un dossier inaccessible n'empêche pas de démarrer : la langue détectée
                // s'applique quand même et sera ré-écrite au premier changement explicite.
            }
        }

        Localizer.Instance.Culture = UiLanguages.CultureOf(language);
    }

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
