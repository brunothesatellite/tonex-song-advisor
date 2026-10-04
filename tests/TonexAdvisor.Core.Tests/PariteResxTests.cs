using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Garde-fou G5 (phase 2, §13) : les deux ressources de chaînes restent paritaires —
/// mêmes clés, valeurs non vides, mêmes emplacements <c>{0}</c> — quel que soit le
/// volume migré au fil des phases 3 à 10.
/// </summary>
public class PariteResxTests
{
    private static readonly string Neutre = Path.Combine(
        TestPaths.RepoRoot, "src", "TonexAdvisor.App", "Localization", "Strings.resx");

    private static readonly string Fr = Path.Combine(
        TestPaths.RepoRoot, "src", "TonexAdvisor.App", "Localization", "Strings.fr.resx");

    /// <summary>Les données de la ressource : nom → valeur (les resheaders sont ignorés).</summary>
    private static Dictionary<string, string> Lire(string fichier)
    {
        var document = XDocument.Load(fichier);

        return document.Root!
            .Elements("data")
            .Where(element => element.Attribute("name") is not null)
            .ToDictionary(
                element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty,
                StringComparer.Ordinal);
    }

    /// <summary>Indices des emplacements d'une valeur (<c>{0:N0}</c> → 0), format ignoré.</summary>
    private static HashSet<int> Emplacements(string valeur)
        => Regex.Matches(valeur, @"\{(\d+)(?::[^{}]*)?\}")
            .Select(match => int.Parse(match.Groups[1].Value))
            .ToHashSet();

    [Fact]
    public void Les_jeux_de_cles_FR_et_EN_s_identiques()
    {
        var anglais = Lire(Neutre);
        var francais = Lire(Fr);

        Assert.NotEmpty(anglais);
        Assert.NotEmpty(francais);

        Assert.Empty(anglais.Keys.Except(francais.Keys).OrderBy(key => key));
        Assert.Empty(francais.Keys.Except(anglais.Keys).OrderBy(key => key));
    }

    [Fact]
    public void Aucune_valeur_des_deux_langues_n_est_vide()
    {
        var vides = Lire(Neutre)
            .Concat(Lire(Fr))
            .Where(entry => string.IsNullOrWhiteSpace(entry.Value))
            .Select(entry => entry.Key)
            .OrderBy(key => key);

        Assert.Empty(vides);
    }

    [Fact]
    public void Les_emplacements_sont_identiques_dans_les_deux_langues()
    {
        var anglais = Lire(Neutre);
        var francais = Lire(Fr);

        var divergences = anglais
            .Where(entry => francais.ContainsKey(entry.Key))
            .Where(entry =>
            {
                var attendus = Emplacements(entry.Value);
                var obtenus = Emplacements(francais[entry.Key]);
                return !attendus.SetEquals(obtenus);
            })
            .Select(entry =>
                $"{entry.Key} : EN [{string.Join(", ", Emplacements(entry.Value).OrderBy(i => i))}] " +
                $"vs FR [{string.Join(", ", Emplacements(francais[entry.Key]).OrderBy(i => i))}]")
            .OrderBy(message => message);

        Assert.Empty(divergences);
    }
}
