using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Resources;

namespace TonexAdvisor.App.Localization;

/// <summary>
/// Socle de localisation (phase 8, §5) : une seule table de clés, deux fichiers —
/// <c>Strings.resx</c> (neutre = anglais) et <c>Strings.fr.resx</c> (français satellite).
/// Aucune dépendance Avalonia : le service reste testable en headless.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    /// <summary>Instance unique de l'application.</summary>
    public static Localizer Instance { get; } = new();

    private static readonly ResourceManager Strings =
        new("TonexAdvisor.App.Localization.Strings", typeof(Localizer).Assembly);

    private CultureInfo _culture = CultureInfo.GetCultureInfo("fr-FR");

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Élevé après chaque changement de <see cref="Culture"/> — pour les abonnés hors INPC
    /// (phasage du démarrage, phase 9, §11).
    /// </summary>
    public event Action? CultureChanged;

    /// <summary>
    /// Culture de l'interface. Poser la propriété cadre les quatre cultures .NET (§11) et,
    /// si la valeur change, notifie l'indexeur <c>Item[]</c> : tous les <c>{loc:Loc}</c>
    /// se rechargent instantanément, sans redémarrage (§1, non-négociable 3).
    /// </summary>
    public CultureInfo Culture
    {
        get => _culture;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            var changed = !Equals(_culture, value);
            _culture = value;
            CultureInfo.CurrentCulture = value;
            CultureInfo.CurrentUICulture = value;
            CultureInfo.DefaultThreadCurrentCulture = value;
            CultureInfo.DefaultThreadCurrentUICulture = value;
            if (!changed)
            {
                return;
            }

            // « Item[] » = convention WPF retenue par le plan (§5) ; « Item » = le nom
            // qu'Avalonia 12 écoute réellement sur une source INPC (mesuré en phase 1,
            // grille de diagnostic) — les deux sont levés, le rechargement est garanti.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item"));
            CultureChanged?.Invoke();
        }
    }

    /// <summary>
    /// Valeur localisée de la clé : culture courante → repli neutre (anglais) → la clé
    /// elle-même ; jamais vide, jamais d'exception (§5).
    /// </summary>
    public string this[string key] => Get(key);

    /// <summary>Valeur localisée brute, sans mise au plat des arguments.</summary>
    public string Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        var value = Strings.GetString(key, _culture);
        if (value is not null)
        {
            return value;
        }

#if DEBUG
        // Clé inconnue = bug d'inventaire : échec visible en debug, mais jamais d'écran
        // cassé ni de boucle inférieure en exécution normale (§5). Non testé sous xunit
        // au regard des boîtes de dialogue de Debug.Fail. Message dev en anglais (phase 10).
        Debug.Fail($"Unknown localization key: '{key}' (missing from the resx?).");
#endif
        return key;
    }

    /// <summary>
    /// Valeur localisée mise au plat : <c>string.Format</c> calé sur la culture courante,
    /// donc <c>{0:N0}</c> sépare les milliers en « 2 310 » (fr) ou « 2,310 » (en) (§6).
    /// </summary>
    public string Get(string key, params object?[] args)
    {
        var format = Get(key);
        return args.Length == 0 ? format : string.Format(_culture, format, args);
    }

    /// <summary>
    /// Valeur localisée d'une culture <b>explicite</b>, indépendante de la culture courante :
    /// traduire une clé historique (§8.2) demande l'en-tête français <i>et</i> l'en-tête
    /// anglais pendant que l'interface n'en affiche qu'un seul. Repli sur la clé elle-même,
    /// jamais vide, jamais d'exception (§5) — la clé inconnue est déjà signalée en debug par
    /// l'appel de validation côté table (voir <c>ColumnIds</c>).
    /// </summary>
    public string GetForCulture(string key, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(culture);
        return Strings.GetString(key, culture) ?? key;
    }

    /// <summary>
    /// Pluriel CLDR (§6) : lit <c>{baseKey}.one</c> ou <c>{baseKey}.other</c> selon la règle
    /// de la langue courante ; repli sur <c>.other</c> si le singulier manque.
    /// Ajouter une langue = ajouter sa règle dans <see cref="EstSingulier"/>.
    /// <c>{0}</c> porte toujours <c>n</c> ; les éventuels <c>args</c> complètent la phrase
    /// (<c>{1}</c>, <c>{2}</c>…), pour les clés à plus d'un argument (« 1 base trouvée dans … »).
    /// </summary>
    public string Plural(int n, string baseKey, params object?[] args)
    {
        ArgumentNullException.ThrowIfNull(baseKey);
        var other = Strings.GetString(baseKey + ".other", _culture);
        var value = EstSingulier(n, _culture)
            ? Strings.GetString(baseKey + ".one", _culture) ?? other
            : other;

        if (value is not null)
        {
            // Mise au plat immédiate : l'appelant reçoit la phrase prête à afficher.
            object?[] arguments = [n, .. args];
            return string.Format(_culture, value, arguments);
        }

#if DEBUG
        Debug.Fail($"Missing plural pair: '{baseKey}.one / {baseKey}.other' (§6).");
#endif
        return baseKey;
    }

    /// <summary>
    /// Règle de pluriel de la langue (§6) : le français met 0 et 1 au singulier ;
    /// toute autre langue suit la règle anglaise (repli par défaut, §17).
    /// </summary>
    private static bool EstSingulier(int n, CultureInfo culture) => culture.TwoLetterISOLanguageName switch
    {
        "fr" => n is 0 or 1,
        _ => n == 1,
    };
}
