using System.Globalization;

namespace TonexAdvisor.Setup;

/// <summary>
/// Table FR/EN des messages de l'installateur — décision 2 (§17) : un tableau en <b>code</b>,
/// pas des ressources, parce que l'installateur se publie en <c>PublishSingleFile=true</c> et
/// que les assemblies satellites ne peuvent pas l'accompagner. La langue suit la règle même de
/// l'application (§11, §17 n°3) : culture OS « fr » → français, tout le reste → anglais.
/// </summary>
/// <remarks>
/// Ce fichier est aussi compilé tel quel dans les tests (lien depuis le projet de tests) : le
/// test de couverture G10 y vérifie que chaque message existe en FR <b>et</b> en EN, avec les
/// mêmes emplacements <c>{0}</c>… — aucune chaîne orpheline dans l'installateur.
/// </remarks>
internal static class SetupTexts
{
    /// <summary>clé → (français, anglais) : les deux langues de chaque message affichable.</summary>
    internal static IReadOnlyDictionary<string, (string Fr, string En)> Table { get; } =
        new Dictionary<string, (string Fr, string En)>(StringComparer.Ordinal)
        {
            ["echec"] = ("Échec : {0}", "Error: {0}"),
            ["installation.dans"] = ("Installation de {0} dans {1}…", "Installing {0} into {1}…"),
            ["charge.absente"] = ("{0} est absent de la charge utile.", "{0} is missing from the payload."),
            ["raccourci.description"] = (
                "Conseille le meilleur preset TONEX pour une chanson.",
                "Advises the best TONEX preset for a song."),
            ["installation.terminee"] = ("Installation terminée.", "Installation complete."),
            ["raccourcis.crees"] = (
                "Raccourcis créés : menu Démarrer et bureau.",
                "Shortcuts created: Start menu and desktop."),
            ["desinstallation.de"] = ("Désinstallation de {0}…", "Uninstalling {0}…"),
            ["desinstallation.terminee"] = (
                "Désinstallation terminée. Vos réglages (%APPDATA%\\TonexAdvisor) sont conservés.",
                "Uninstall complete. Your settings (%APPDATA%\\TonexAdvisor) are kept."),
            ["appuyez.une.touche"] = ("Appuyez sur une touche pour fermer.", "Press any key to close."),
            ["charge.utile.absente"] = (
                "Charge utile absente : construisez l'installateur avec tools\\build-release.ps1.",
                "Payload missing: build the installer with tools\\build-release.ps1."),
            ["fichiers.extraits"] = ("{0} fichiers extraits.", "{0} files extracted."),
            ["wscript.indisponible"] = ("WScript.Shell indisponible.", "WScript.Shell unavailable."),
            ["cle.impossible"] = (
                "Impossible d'écrire la clé de désinstallation.",
                "Cannot write the uninstall registry key."),
        };

    /// <summary>
    /// Même règle que l'application (§11) : « fr » renvoie au français, tout le reste — en,
    /// de, es, pt… — bascule en anglais (repli anglais, décision 3).
    /// </summary>
    internal static bool UseFrench(CultureInfo? os = null)
        => (os ?? CultureInfo.CurrentUICulture).TwoLetterISOLanguageName == "fr";

    /// <summary>Message de la clé, dans la langue de l'OS, arguments rendus dans cette culture.</summary>
    internal static string Get(string key, params object?[] args)
        => Get(key, os: null, args);

    /// <inheritdoc cref="Get(string, object[])"/>
    internal static string Get(string key, CultureInfo? os, params object?[] args)
    {
        var culture = os ?? CultureInfo.CurrentUICulture;

        if (!Table.TryGetValue(key, out var message))
            throw new KeyError($"Message d'installateur absent de la table : '{key}'.");

        var texte = UseFrench(culture) ? message.Fr : message.En;
        return args.Length == 0 ? texte : string.Format(culture, texte, args);
    }
}

/// <summary>Clé de la table absente : bug de développement, jamais une console muette.</summary>
internal sealed class KeyError(string message) : Exception(message);
