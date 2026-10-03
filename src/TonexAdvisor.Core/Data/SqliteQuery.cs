using Microsoft.Data.Sqlite;

namespace TonexAdvisor.Core.Data;

/// <summary>
/// Thin helper executing read-only statements and handing each row to a mapper.
/// </summary>
internal static class SqliteQuery
{
    public static List<T> Select<T>(SqliteConnection connection, string sql, Func<SqliteRow, T> map)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ReadOnlyConnection.EnsureReadOnly(sql);
        ArgumentNullException.ThrowIfNull(map);

        var results = new List<T>();

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        using var reader = command.ExecuteReader();
        var index = BuildIndex(reader);
        while (reader.Read())
            results.Add(map(new SqliteRow(reader, index)));

        return results;
    }

    /// <summary>Runs a statement whose result set is not needed.</summary>
    public static void Execute(SqliteConnection connection, string sql)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ReadOnlyConnection.EnsureReadOnly(sql);

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static Dictionary<string, int> BuildIndex(SqliteDataReader reader)
    {
        var index = new Dictionary<string, int>(reader.FieldCount, StringComparer.OrdinalIgnoreCase);
        for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
        {
            var name = reader.GetName(ordinal);
            if (!string.IsNullOrEmpty(name))
                index[name] = ordinal;
        }

        return index;
    }
}
