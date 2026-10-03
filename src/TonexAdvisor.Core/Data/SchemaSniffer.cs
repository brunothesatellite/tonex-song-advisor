using Microsoft.Data.Sqlite;

namespace TonexAdvisor.Core.Data;

/// <summary>
/// Detects which TONEX library generation a database file contains, and enumerates its columns.
/// </summary>
public static class SchemaSniffer
{
    /// <summary>Detects the format of an already-open connection.</summary>
    public static DatabaseFormat Detect(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var presetColumns = ColumnsOf(connection, "Presets");
        if (presetColumns.Count == 0)
            return DatabaseFormat.Unknown;

        // V1 keeps the tone model foreign key directly on the preset row.
        if (presetColumns.Contains("ToneModel_GUID", StringComparer.OrdinalIgnoreCase))
            return DatabaseFormat.V1;

        // V2 replaces parameters and the foreign key with a chain description.
        if (presetColumns.Contains("Chain", StringComparer.OrdinalIgnoreCase))
            return DatabaseFormat.V2;

        return DatabaseFormat.Unknown;
    }

    /// <summary>Detects the format of a database file without holding it open.</summary>
    public static DatabaseFormat Detect(string databasePath)
    {
        ArgumentNullException.ThrowIfNull(databasePath);
        using var connection = ReadOnlyConnection.Open(databasePath);
        return Detect(connection);
    }

    /// <summary>Returns the declared column names of <paramref name="table"/>, or an empty list.</summary>
    public static IReadOnlySet<string> ColumnsOf(SqliteConnection connection, string table)
    {
        ArgumentNullException.ThrowIfNull(connection);
        if (string.IsNullOrWhiteSpace(table))
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Table names come from our own code, never from user input, but quote anyway so the
        // helper stays safe if it is ever reused.
        var quoted = table.Replace("\"", "\"\"", StringComparison.Ordinal);
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{quoted}\")";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var name = reader.GetString(1);
            if (!string.IsNullOrEmpty(name))
                columns.Add(name);
        }

        return columns;
    }

    /// <summary>Returns the names of every user table in the database.</summary>
    public static IReadOnlyList<string> TablesOf(SqliteConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
        var tables = new List<string>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
                tables.Add(reader.GetString(0));
        }

        return tables;
    }

    /// <summary>True when the database contains <paramref name="table"/>.</summary>
    public static bool HasTable(SqliteConnection connection, string table)
        => TablesOf(connection).Contains(table, StringComparer.OrdinalIgnoreCase);
}
