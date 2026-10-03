using Microsoft.Data.Sqlite;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Data.Readers;

/// <summary>
/// Reads <c>Library.db</c> tone models. The encrypted <c>Model</c>/<c>CabModel</c> payloads are
/// never selected: they are large, opaque and useless to the advisor.
/// </summary>
public sealed class V1ToneModelReader : IToneModelReader
{
    private readonly SqliteConnection _connection;

    public V1ToneModelReader(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public IReadOnlyList<ToneModelRecord> ReadToneModels()
    {
        var folders = LibraryLookups.ToneModelFolders(_connection);
        var authors = LibraryLookups.Authors(_connection);
        var collections = LibraryLookups.Collections(_connection);

        return SqliteQuery.Select(
            _connection,
            "SELECT GUID, Target, TargetOrder, Skin, Instrument, Copyright, " +
            "Tag_ModelName, Tag_Keywords, Tag_Description, Tag_ModelCategory, " +
            "Tag_AmpName, Tag_StompName, Tag_AmpChannel, Tag_ModelComment, " +
            "Tag_CabCategory, Tag_CabName, Tag_CabMic1, Tag_CabMic2, " +
            "Tag_Outboard, Tag_CabModelComment, DateAdded, Favorite " +
            "FROM ToneModels",
            row => Map(row, folders, authors, collections));
    }

    internal static ToneModelRecord Map(
        SqliteRow row,
        IReadOnlyDictionary<string, List<string>> folders,
        IReadOnlyDictionary<string, string> authors,
        IReadOnlyDictionary<string, string> collections)
    {
        var guid = row.String("GUID").Trim();

        return new ToneModelRecord
        {
            Key = guid,
            Guid = guid,
            Name = row.String("Tag_ModelName").Trim(),
            AmpName = row.String("Tag_AmpName").Trim(),
            StompName = row.String("Tag_StompName").Trim(),
            AmpChannel = row.String("Tag_AmpChannel").Trim(),
            Category = row.String("Tag_ModelCategory").Trim(),
            CabCategory = row.String("Tag_CabCategory").Trim(),
            CabName = row.String("Tag_CabName").Trim(),
            Mic1 = row.String("Tag_CabMic1").Trim(),
            Mic2 = row.String("Tag_CabMic2").Trim(),
            Outboard = row.String("Tag_Outboard").Trim(),
            Keywords = row.String("Tag_Keywords").Trim(),
            Description = row.String("Tag_Description").Trim(),
            ModelComment = row.String("Tag_ModelComment").Trim(),
            CabComment = row.String("Tag_CabModelComment").Trim(),
            Skin = row.String("Skin").Trim(),
            DateAdded = row.String("DateAdded").Trim(),
            Favorite = row.Flag("Favorite"),
            Copyright = row.String("Copyright").Trim(),
            Author = LibraryLookups.Resolve(authors, guid),
            Kind = ToneModelMapping.ToKind(row.Int("Target", 3)),
            KindOrder = row.String("TargetOrder").Trim(),
            Folders = LibraryLookups.OrEmpty(folders, guid),
            Collection = collections.TryGetValue(guid, out var collection) ? collection : null,
        };
    }
}

/// <summary>Converts the numeric <c>Target</c> column into <see cref="ToneModelKind"/>.</summary>
internal static class ToneModelMapping
{
    public static ToneModelKind ToKind(int target)
        => target is >= 0 and <= 5 ? (ToneModelKind)target : ToneModelKind.AmpAndCab;
}
