using Microsoft.Data.Sqlite;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Data.Readers;

/// <summary>
/// Shared lookup tables that both generations keep in the same shape: folders, ToneNET
/// nicknames and premium collections.
/// </summary>
internal static class LibraryLookups
{
    public static IReadOnlyDictionary<string, List<string>> PresetFolders(SqliteConnection connection)
        => Grouped(
            connection,
            "SELECT PresetName, FolderName FROM PresetFolderAssociations",
            nameColumn: "PresetName",
            folderColumn: "FolderName");

    public static IReadOnlyDictionary<string, List<string>> ToneModelFolders(SqliteConnection connection)
        => Grouped(
            connection,
            "SELECT ToneModelGUID, FolderName FROM ToneModelFolderAssociations",
            nameColumn: "ToneModelGUID",
            folderColumn: "FolderName");

    /// <summary>Maps a tone model GUID to its resolved ToneNET nickname.</summary>
    public static IReadOnlyDictionary<string, string> Authors(SqliteConnection connection)
    {
        var users = SqliteQuery.Select(
            connection,
            "SELECT UserID, Nickname FROM UserIDNicknameMatch",
            row => (Id: row.String("UserID"), Nickname: row.String("Nickname")))
            .Where(pair => pair.Id.Length > 0)
            .GroupBy(pair => pair.Id)
            .ToDictionary(group => group.Key, group => group.First().Nickname, StringComparer.OrdinalIgnoreCase);

        var matches = SqliteQuery.Select(
            connection,
            "SELECT GUID, UserID FROM ToneModelsUserIDMatch",
            row => (Guid: row.String("GUID"), UserId: row.String("UserID")));

        var authors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var match in matches)
        {
            if (match.Guid.Length == 0)
                continue;
            if (users.TryGetValue(match.UserId, out var nickname) && nickname.Length > 0)
                authors[match.Guid] = nickname;
        }

        return authors;
    }

    /// <summary>Maps a tone model GUID to the premium collection it belongs to.</summary>
    public static IReadOnlyDictionary<string, string> Collections(SqliteConnection connection)
    {
        if (!SchemaSniffer.HasTable(connection, "CollectionToneModels")
            || !SchemaSniffer.HasTable(connection, "CollectionData"))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var names = SqliteQuery.Select(
            connection,
            "SELECT Code, Name FROM CollectionData",
            row => (Code: row.String("Code"), Name: row.String("Name")))
            .Where(pair => pair.Code.Length > 0 && pair.Name.Length > 0)
            .GroupBy(pair => pair.Code)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

        var links = SqliteQuery.Select(
            connection,
            "SELECT CollectionCode, GUID FROM CollectionToneModels",
            row => (Code: row.String("CollectionCode"), Guid: row.String("GUID")));

        var collections = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var link in links)
        {
            if (link.Guid.Length == 0 || !names.TryGetValue(link.Code, out var name))
                continue;
            collections.TryAdd(link.Guid, name);
        }

        return collections;
    }

    private static IReadOnlyDictionary<string, List<string>> Grouped(
        SqliteConnection connection,
        string sql,
        string nameColumn,
        string folderColumn)
    {
        var rows = SqliteQuery.Select(
            connection,
            sql,
            row => (Name: row.String(nameColumn), Folder: row.String(folderColumn)));

        var lookup = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            if (row.Name.Length == 0 || row.Folder.Length == 0)
                continue;

            if (!lookup.TryGetValue(row.Name, out var folders))
                lookup[row.Name] = folders = new List<string>();

            if (!folders.Contains(row.Folder, StringComparer.OrdinalIgnoreCase))
                folders.Add(row.Folder);
        }

        return lookup;
    }

    /// <summary>Splits a possibly-null lookup into an empty list.</summary>
    public static IReadOnlyList<string> OrEmpty(IReadOnlyDictionary<string, List<string>> lookup, string key)
        => lookup.TryGetValue(key, out var folders) ? folders : Array.Empty<string>();

    /// <summary>Resolves a GUID through a dictionary, defaulting to an empty string.</summary>
    public static string Resolve(IReadOnlyDictionary<string, string> lookup, string key)
        => lookup.TryGetValue(key, out var value) ? value : "";
}
