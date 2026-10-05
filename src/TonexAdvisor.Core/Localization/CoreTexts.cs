using System.Globalization;
using System.Resources;

namespace TonexAdvisor.Core.Localization;

/// <summary>
/// Textes construits en Core : invites du Conseil (§8.1), raisons de classement, libellés de
/// potards, origines jointes. Core ne connaît pas le <c>Localizer</c> de l'App — la langue
/// entre par paramètre quand l'appelant en a un (les invites reçoivent la culture de la
/// session), sinon par la culture ambiante que l'App cadre au démarrage (§11). Repli sur la
/// clé elle-même : jamais vide, jamais d'exception (§5).
/// </summary>
/// <remarks>
/// Décision phase 10 : paire de ressources Core (neutre EN + satellite fr), extraction
/// verbatim des deux branches déjà en code — les tests FR d'invites restent verts sans
/// modification. Parité FR/EN couverte par le même garde-fou que l'App.
/// </remarks>
public static class CoreTexts
{
    private static readonly ResourceManager Strings =
        new("TonexAdvisor.Core.Localization.Strings", typeof(CoreTexts).Assembly);

    /// <summary>Texte de la clé dans la culture demandée (défaut : culture ambiante cadrée).</summary>
    public static string Get(string key, CultureInfo? culture = null)
    {
        ArgumentNullException.ThrowIfNull(key);
        return Strings.GetString(key, culture ?? CultureInfo.CurrentUICulture) ?? key;
    }

    /// <summary>Texte de la clé avec ses arguments rendus dans la culture ({0}, {1}…).</summary>
    public static string Format(string key, CultureInfo? culture = null, params object?[] args)
    {
        var texte = Get(key, culture);
        return args.Length == 0
            ? texte
            : string.Format(culture ?? CultureInfo.CurrentUICulture, texte, args);
    }

    /// <summary>
    /// Pluriel CLDR (§6), en miroir du <c>Localizer</c> sans en dépendre : la règle de la
    /// culture dit si l'on lit <c>{baseKey}.one</c> ou <c>{baseKey}.other</c> — fr : 0 et 1
    /// au singulier ; en : 1 seul — repli sur le pluriel, puis sur la clé (§5).
    /// <c>{0}</c> porte toujours <c>n</c> ; les <c>args</c> complètent ({1}, {2}…).
    /// </summary>
    public static string Plural(int n, string baseKey, CultureInfo? culture = null, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(baseKey);

        var cible = culture ?? CultureInfo.CurrentUICulture;
        var other = Strings.GetString(baseKey + ".other", cible);
        var value = Singulier(n, cible)
            ? Strings.GetString(baseKey + ".one", cible) ?? other
            : other;

        if (value is null)
            return baseKey;

        object?[] arguments = [n, .. args];
        return string.Format(cible, value, arguments);
    }

    /// <summary>Règle de singulier : fr = {0, 1}, toute autre langue = {1} — identique au Localizer.</summary>
    private static bool Singulier(int n, CultureInfo culture)
        => culture.Name.StartsWith("fr", StringComparison.OrdinalIgnoreCase) ? n <= 1 : n == 1;
}
