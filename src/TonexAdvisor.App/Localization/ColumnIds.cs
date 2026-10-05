using System;
using System.Collections.Generic;
using System.Globalization;

namespace TonexAdvisor.App.Localization;

/// <summary>
/// Largeurs de colonnes en identifiants stables (§8.2, piège n°1) : la clé persistée dans
/// <c>state.json</c> ne dépend jamais de la langue affichée. Chaque colonne porte un id
/// anglais (« name », « category »…) relié à la clé de localisation de son en-tête ; une clé
/// historique — l'en-tête français ou anglais écrit par la v1.2.1 — se traduit en id ici et
/// se ré-écrit au prochain enregistrement (lecture-modification-écriture déjà en place).
/// </summary>
public static class ColumnIds
{
    /// <summary>Les deux grilles qui mémorisent des largeurs.</summary>
    public enum Grid
    {
        Presets,
        ToneModels,
    }

    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

    // Chaque entrée passe par Key(Localizer.Instance, …) : la clé est validée chez le
    // Localizer au chargement (§5 — une clé renommée casse en debug, pas en silence), et la
    // ligne se lit comme une référence de clé pour le scan G6 : un en-tête de colonne n'est
    // jamais un littéral d'affichage à compter.
    private static readonly (string Id, string Key)[] PresetColumns =
    [
        ("fav", Key(Localizer.Instance, "Biblio.Col.Fav")),
        ("name", Key(Localizer.Instance, "Biblio.Col.Nom")),
        ("category", Key(Localizer.Instance, "Biblio.Col.Categorie")),
        ("genre", Key(Localizer.Instance, "Biblio.Col.Genre")),
        ("artist", Key(Localizer.Instance, "Biblio.Col.Artiste")),
        ("song", Key(Localizer.Instance, "Biblio.Col.Chanson")),
        ("stomp", Key(Localizer.Instance, "Biblio.Col.Stomp")),
        ("amp", Key(Localizer.Instance, "Biblio.Col.Ampli")),
        ("cab", Key(Localizer.Instance, "Biblio.Col.Baffle")),
        ("settings", Key(Localizer.Instance, "Biblio.Col.Reglages")),
    ];

    private static readonly (string Id, string Key)[] ToneModelColumns =
    [
        ("fav", Key(Localizer.Instance, "Biblio.Col.Fav")),
        ("name", Key(Localizer.Instance, "Biblio.Col.Nom")),
        ("stomp", Key(Localizer.Instance, "Biblio.Col.Stomp")),
        ("amp", Key(Localizer.Instance, "Biblio.Col.Ampli")),
        ("cab", Key(Localizer.Instance, "Biblio.Col.Baffle")),
        ("mics", Key(Localizer.Instance, "Biblio.Col.Micros")),
        ("category", Key(Localizer.Instance, "Biblio.Col.Categorie")),
        ("kind", Key(Localizer.Instance, "Biblio.Col.Type")),
        ("presets", Key(Localizer.Instance, "Biblio.Presets")),
    ];

    private static readonly Dictionary<string, string> PresetIndex = Index(PresetColumns);
    private static readonly Dictionary<string, string> ToneModelIndex = Index(ToneModelColumns);

    /// <summary>
    /// Id d'une clé persistée : l'id lui-même, la clé de localisation de l'en-tête, ou
    /// l'en-tête FR/EN de la v1.2.1 (compat §8.2) ; <c>null</c> quand rien ne correspond.
    /// </summary>
    public static string? IdOf(Grid grid, string storedKey)
    {
        ArgumentNullException.ThrowIfNull(storedKey);
        return IndexOf(grid).TryGetValue(storedKey, out var id) ? id : null;
    }

    /// <summary>
    /// En-tête de la culture courante pour une clé persistée : c'est ce que la vue compare à
    /// <c>DataGridColumn.Header</c> pour appliquer une largeur — id traduit ou clé historique,
    /// le résultat est le même en français et en anglais.
    /// </summary>
    public static string HeaderOf(Grid grid, string storedKey)
    {
        ArgumentNullException.ThrowIfNull(storedKey);

        var id = IdOf(grid, storedKey) ?? storedKey;
        var columns = grid == Grid.Presets ? PresetColumns : ToneModelColumns;

        foreach (var (candidate, key) in columns)
        {
            if (string.Equals(candidate, id, StringComparison.Ordinal))
                return Localizer.Instance[key];
        }

        return storedKey;
    }

    /// <summary>
    /// Recopie une table de largeurs en y normalisant les clés : les en-têtes historiques
    /// deviennent des ids, les ids passent tels quels, une clé inconnue est conservée — un
    /// fichier écrit par une version plus récente que l'application ne perd jamais ses
    /// largeurs. La conversion se voit au chargement et, une nouvelle fois, à l'écriture.
    /// </summary>
    public static Dictionary<string, double> Normalize(Grid grid, IReadOnlyDictionary<string, double> widths)
    {
        ArgumentNullException.ThrowIfNull(widths);

        var normalized = new Dictionary<string, double>(widths.Count, StringComparer.Ordinal);
        foreach (var (key, pixels) in widths)
            normalized[IdOf(grid, key) ?? key] = pixels;

        return normalized;
    }

    private static Dictionary<string, string> IndexOf(Grid grid)
        => grid == Grid.Presets ? PresetIndex : ToneModelIndex;

    /// <summary>
    /// id + clé + en-tête FR + en-tête EN → id : la traduction d'une clé historique ne
    /// dépend d'aucune culture courante, l'une des deux langues restant toujours trouvable.
    /// </summary>
    private static Dictionary<string, string> Index((string Id, string Key)[] columns)
    {
        var index = new Dictionary<string, string>(columns.Length * 4, StringComparer.Ordinal);

        foreach (var (id, key) in columns)
        {
            index[id] = id;
            index[key] = id;
            index[Localizer.Instance.GetForCulture(key, Fr)] = id;
            index[Localizer.Instance.GetForCulture(key, En)] = id;
        }

        return index;
    }

    /// <summary>
    /// La clé, validée chez le Localizer : la table range l'identifiant de la ressource, pas
    /// la valeur du jour — rien ne dérive du fichier, et une clé renommée casse en debug.
    /// </summary>
    private static string Key(Localizer localizer, string key)
    {
        _ = localizer[key];
        return key;
    }
}
