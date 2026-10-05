using System.Text.RegularExpressions;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Garde-fou G6 (phase 2, §13) : scan des littéraux en dur (XAML + C#) sous <c>src</c>,
/// confronté à une allowlist par fichier (<c>litteraux-allowlist.txt</c>). La règle est
/// identique à <c>tools\inventaire-i18n.ps1</c> (phase 0) — même couverture que
/// l'inventaire. Toute chaîne ajoutée **ou** retirée décale un compteur et impose une
/// mise à jour consciente de l'allowlist ; l'allowlist doit devenir vide en phase 10.
/// </summary>
public class ScanLitterauxTests
{
    // Miroir exact des règles de tools\inventaire-i18n.ps1 — ne pas diverger.
    private const string AttributsXaml =
        @"\s(?:Text|Header|Content|ToolTip\.Tip|Watermark|PlaceholderText)=""([^""{][^""]*)""";

    // Affinage phase 10 (mesure à l'appui, §18) : sept marqueurs qui ne servaient qu'au
    // hors-champ technique (SQL « Presets »/« Favorite », chemins « TonexAdvisor », user-agent,
    // « TextBlock » pris pour BLOC, sentinelle « Tous », vocabulaire, `mod[eè]le` qui mordait
    // les identifiants « ModelEnable ») sont retirés — aucune ligne d'affichage de
    // l'inventaire ne dépendait d'eux seuls. Les marqueurs de texte restants (accents +
    // mots FR non ambigus) couvrent tout l'affichable.
    private const string LigneFrancaise =
        "[éèêàçùôîï«»]|Aucun|Aucune|Echec|Échec|\\bcl[ée]s?\\b|" +
        "dossier|catég|régl|Conseil|conseil|arbitre|voix|lecture|Recherche|recherche|Réinitialiser|" +
        "gratuit|payant|SUGGESTION|VERDICT|BAFFLE|CONSEIL LIBRE|joint|" +
        "extraits|touche|Installation|Désinstallation|charg|\\bVous\\b|\\bvotre\\b|\\bVotre\\b|" +
        "fichier|Délai|indisponible|Annuler|annuler|preset\\(s\\)";

    private const string LitteralCsharp = @"""([^""\\]*(?:\\.[^""\\]*)*)""";

    private const string LigneCommentaire = @"^\s*(//|/\*|\*)";

    private const string NomAllowlist = "litteraux-allowlist.txt";

    /// <summary>Chemin du fichier d'allowlist (relatif au dépôt).</summary>
    public static string CheminAllowlist => Path.Combine(
        TestPaths.RepoRoot, "tests", "TonexAdvisor.Core.Tests", NomAllowlist);

    /// <summary>Littéraux en dur sous src : chemin relatif (dépôt) → nombre trouvés.</summary>
    public static Dictionary<string, int> Scanner()
    {
        var racine = TestPaths.RepoRoot;
        var src = Path.Combine(racine, "src");
        var compte = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var fichier in Fichiers(src, "*.axaml"))
        {
            var nombre = 0;

            foreach (var ligne in File.ReadAllLines(fichier))
            {
                foreach (Match match in Regex.Matches(ligne, AttributsXaml))
                {
                    if (Regex.IsMatch(match.Groups[1].Value.Trim(), "[A-Za-zÀ-ÿ]"))
                        nombre++;
                }
            }

            if (nombre > 0)
                compte[Relatif(racine, fichier)] = nombre;
        }

        foreach (var fichier in Fichiers(src, "*.cs"))
        {
            var nombre = 0;

            foreach (var ligne in File.ReadAllLines(fichier))
            {
                if (Regex.IsMatch(ligne, LigneCommentaire))
                    continue;
                // Références de clés (Localizer.Get/Instance[...], CoreTexts.Get/Format[...]) :
                // une clé n'est pas une chaîne d'affichage. Cette exclusion existe aussi dans le
                // script d'inventaire — miroir préservé.
                if (ligne.Contains("Localizer.", StringComparison.Ordinal)
                    || ligne.Contains("CoreTexts.", StringComparison.Ordinal))
                    continue;
                // IgnoreCase : miroir exact de « -match » (PowerShell, insensible à la casse).
                if (!Regex.IsMatch(ligne, LigneFrancaise, RegexOptions.IgnoreCase))
                    continue;

                foreach (Match match in Regex.Matches(ligne, LitteralCsharp))
                {
                    if (match.Groups[1].Value.Length >= 2)
                        nombre++;
                }
            }

            if (nombre > 0)
                compte[Relatif(racine, fichier)] = nombre;
        }

        return compte;
    }

    /// <summary>Allowlist : chemin relatif → nombre de littéraux autorisés.</summary>
    private static Dictionary<string, int> LireAllowlist()
    {
        var entrees = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var ligne in File.ReadAllLines(CheminAllowlist))
        {
            if (ligne.Length == 0 || ligne.StartsWith('#'))
                continue;

            var parts = ligne.Split(';');
            entrees[parts[0]] = int.Parse(parts[1]);
        }

        return entrees;
    }

    private static IEnumerable<string> Fichiers(string src, string pattern)
        => Directory.EnumerateFiles(src, pattern, SearchOption.AllDirectories)
            .Where(fichier => !fichier.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                           && !fichier.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

    private static string Relatif(string racine, string fichier)
        => fichier.Substring(racine.Length + 1);

    [Fact]
    public void Aucun_litteral_hors_allowlist_ni_en_exces()
    {
        var reel = Scanner();
        var autorise = LireAllowlist();

        // Garde contre un scan vide (chemin src cassé) : le fichier d'allowlist existe, et la
        // phase 10 l'a vidé — il ne doit plus jamais se remplir (G6).
        Assert.True(File.Exists(CheminAllowlist), "l'allowlist du scan doit exister, même vide");

        var ecarts = reel
            .Where(entry => !autorise.TryGetValue(entry.Key, out var nombre) || nombre != entry.Value)
            .Select(entry => autorise.TryGetValue(entry.Key, out var nombre)
                ? $"{entry.Key} : allowlist {nombre}, scan {entry.Value}"
                : $"{entry.Key} : hors allowlist ({entry.Value} littéraux)")
            .OrderBy(message => message);

        Assert.Empty(ecarts);
    }

    [Fact]
    public void Allowlist_sans_entree_perimee()
    {
        var reel = Scanner();

        var perimees = LireAllowlist()
            .Where(entry => !reel.ContainsKey(entry.Key))
            .Select(entry => $"{entry.Key} : allowlist {entry.Value}, scan 0 (entrée à retirer)")
            .OrderBy(message => message);

        Assert.Empty(perimees);
    }
}
