using Microsoft.Data.Sqlite;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Data.Readers;

/// <summary>
/// Reads <c>Library2.db</c> presets. Generation 2 stores no knob values at all, only the tag
/// columns, the chain description and the link rows pointing at tone models.
/// </summary>
public sealed class V2PresetReader : IPresetReader
{
    private readonly SqliteConnection _connection;

    public V2PresetReader(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public IReadOnlyList<PresetRecord> ReadPresets()
    {
        var folders = LibraryLookups.PresetFolders(_connection);
        var links = ReadLinks();

        return SqliteQuery.Select(
            _connection,
            "SELECT ID, Chain, Tag_PresetName, Tag_UserName, Tag_ModelCategory, Tag_Genre, " +
            "Tag_Artist, Tag_Album, Tag_Song, Tag_Description, Tag_Instrument, Tag_Date, " +
            "Favorite FROM Presets",
            row => Map(row, folders, links));
    }

    /// <summary>Builds the preset ID to tone model ID map from the join table.</summary>
    private IReadOnlyDictionary<int, List<string>> ReadLinks()
    {
        if (!SchemaSniffer.HasTable(_connection, "PresetsToneModelsMatch"))
            return new Dictionary<int, List<string>>();

        var rows = SqliteQuery.Select(
            _connection,
            "SELECT PresetID, ToneModelID FROM PresetsToneModelsMatch",
            row => (PresetId: row.Number("PresetID"), ModelId: row.Number("ToneModelID")));

        var links = new Dictionary<int, List<string>>();
        foreach (var row in rows)
        {
            if (row.PresetId is null || row.ModelId is null)
                continue;

            var presetId = (int)row.PresetId.Value;
            if (!links.TryGetValue(presetId, out var models))
                links[presetId] = models = new List<string>(2);

            var modelKey = ((int)row.ModelId.Value).ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!models.Contains(modelKey, StringComparer.OrdinalIgnoreCase))
                models.Add(modelKey);
        }

        return links;
    }

    private static PresetRecord Map(
        SqliteRow row,
        IReadOnlyDictionary<string, List<string>> folders,
        IReadOnlyDictionary<int, List<string>> links)
    {
        var name = row.String("Tag_PresetName").Trim();
        var id = row.Int("ID");

        IReadOnlyList<string> toneModels = links.TryGetValue(id, out var found)
            ? found
            : Array.Empty<string>();

        return new PresetRecord
        {
            Key = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Name = name,
            UserName = row.String("Tag_UserName").Trim(),
            Category = row.String("Tag_ModelCategory").Trim(),
            Genre = row.String("Tag_Genre").Trim(),
            Artist = row.String("Tag_Artist").Trim(),
            Album = row.String("Tag_Album").Trim(),
            Song = row.String("Tag_Song").Trim(),
            Description = row.String("Tag_Description").Trim(),
            Instrument = row.String("Tag_Instrument").Trim(),
            DateAdded = row.String("Tag_Date").Trim(),
            Favorite = row.Flag("Favorite"),
            Folders = LibraryLookups.OrEmpty(folders, name),
            ToneModelKeys = toneModels,
            Chain = ChainBlock.ParseList(row.String("Chain")),
            Settings = null,
        };
    }
}
