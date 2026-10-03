using Microsoft.Data.Sqlite;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Data.Readers;

/// <summary>
/// Reads <c>Library2.db</c> tone models. Generation 2 moved the audio payload out of the
/// database entirely, so only metadata is available - which is all the advisor needs.
/// </summary>
public sealed class V2ToneModelReader : IToneModelReader
{
    private readonly SqliteConnection _connection;

    public V2ToneModelReader(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public IReadOnlyList<ToneModelRecord> ReadToneModels()
    {
        if (!SchemaSniffer.HasTable(_connection, "ToneModels"))
            return Array.Empty<ToneModelRecord>();

        var folders = LibraryLookups.ToneModelFolders(_connection);
        var authors = LibraryLookups.Authors(_connection);
        var collections = LibraryLookups.Collections(_connection);

        return SqliteQuery.Select(
            _connection,
            "SELECT ID, GUID, Target, TargetOrder, Skin, Copyright, " +
            "Tag_ModelName, Tag_Keywords, Tag_Description, Tag_ModelCategory, " +
            "Tag_AmpName, Tag_StompName, Tag_AmpChannel, Tag_ModelComment, " +
            "Tag_CabCategory, Tag_CabName, Tag_CabMic1, Tag_CabMic2, " +
            "Tag_Outboard, Tag_CabModelComment, DateAdded, Favorite " +
            "FROM ToneModels",
            row => Map(row, folders, authors, collections));
    }

    private static ToneModelRecord Map(
        SqliteRow row,
        IReadOnlyDictionary<string, List<string>> folders,
        IReadOnlyDictionary<string, string> authors,
        IReadOnlyDictionary<string, string> collections)
    {
        var guid = row.String("GUID").Trim();
        var id = row.Int("ID");

        // The record key must match what V2PresetReader puts in PresetRecord.ToneModelKeys:
        // the integer ID, not the GUID.
        var record = V1ToneModelReader.Map(row, folders, authors, collections);

        return record with
        {
            Key = id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Guid = guid,
        };
    }
}
