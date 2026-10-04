using System.Globalization;
using TonexAdvisor.App.Localization;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Sauvegarde puis restaure les quatre cultures .NET et la culture du Localizer autour
/// d'un test qui bascule volontairement de langue : aucune fuite vers les tests voisins
/// (G3, §11). Le point de départ de chaque test protégé est le français.
/// </summary>
public sealed class CultureGuard : IDisposable
{
    private readonly CultureInfo _current = CultureInfo.CurrentCulture;
    private readonly CultureInfo _currentUi = CultureInfo.CurrentUICulture;
    private readonly CultureInfo? _default = CultureInfo.DefaultThreadCurrentCulture;
    private readonly CultureInfo? _defaultUi = CultureInfo.DefaultThreadCurrentUICulture;
    private readonly CultureInfo _localizer = Localizer.Instance.Culture;

    public CultureGuard()
        => Localizer.Instance.Culture = CultureInfo.GetCultureInfo("fr-FR");

    public void Dispose()
    {
        Localizer.Instance.Culture = _localizer;
        CultureInfo.CurrentCulture = _current;
        CultureInfo.CurrentUICulture = _currentUi;
        CultureInfo.DefaultThreadCurrentCulture = _default;
        CultureInfo.DefaultThreadCurrentUICulture = _defaultUi;
    }
}
