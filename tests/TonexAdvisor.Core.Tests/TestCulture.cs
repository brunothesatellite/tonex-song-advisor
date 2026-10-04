using System.Globalization;
using System.Runtime.CompilerServices;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Cadre toute la suite sur fr-FR (G3, phase 8) : les 195 tests d'origine supposaient
/// implicitement une machine française ; l'exécution devient indépendante de la machine.
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    public static void Frame()
    {
        var fr = CultureInfo.GetCultureInfo("fr-FR");
        CultureInfo.CurrentCulture = fr;
        CultureInfo.CurrentUICulture = fr;
        CultureInfo.DefaultThreadCurrentCulture = fr;
        CultureInfo.DefaultThreadCurrentUICulture = fr;
    }
}
