using System.Collections.Generic;
using System.Globalization;

namespace TonexAdvisor.App.Localization;

/// <summary>
/// Les langues d'interface et leurs règles (§10, §11) : le choix mémorisé dans
/// <c>state.json</c> d'abord, la détection système à défaut — « fr » aux deux lettres renvoie
/// au français, tout le reste bascule en anglais (repli anglais, décision 3). Un choix
/// explicite gagne toujours sur la machine.
/// </summary>
public static class UiLanguages
{
    /// <summary>Français : langue historique de l'application.</summary>
    public const string French = "fr";

    /// <summary>Anglais : langue neutre des ressources, et repli universel.</summary>
    public const string English = "en";

    /// <summary>Les deux codes proposés, dans l'ordre de la ComboBox des réglages.</summary>
    public static IReadOnlyList<string> Codes { get; } = [French, English];

    /// <summary>Vrai pour « fr » et « en » — tout le reste est hors contrat (§10).</summary>
    public static bool IsSupported(string? language)
        => language is French or English;

    /// <summary>
    /// Langue du prochain lancement : le choix explicite s'il existe, sinon la détection —
    /// deux lettres « fr » → français, tout le reste (en, en-US, de, es, pt…) → anglais (§11).
    /// </summary>
    public static string Resolve(string? stored, CultureInfo systemUi)
    {
        ArgumentNullException.ThrowIfNull(systemUi);

        return IsSupported(stored)
            ? stored!
            : systemUi.TwoLetterISOLanguageName == French ? French : English;
    }

    /// <summary>Culture .NET d'une langue d'interface : « fr-FR » ou « en-US » (§11).</summary>
    public static CultureInfo CultureOf(string language)
        => CultureInfo.GetCultureInfo(language == French ? "fr-FR" : "en-US");
}
