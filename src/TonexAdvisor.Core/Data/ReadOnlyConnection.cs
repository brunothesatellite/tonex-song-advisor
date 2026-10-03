using Microsoft.Data.Sqlite;

namespace TonexAdvisor.Core.Data;

/// <summary>
/// Opens TONEX library files in a way that makes writing impossible.
/// </summary>
/// <remarks>
/// Three independent barriers are stacked, because a stray UPDATE against a live library would
/// be unrecoverable for the user:
/// <list type="number">
/// <item><description>the connection string requests <c>Mode=ReadOnly</c>;</description></item>
/// <item><description><c>PRAGMA query_only = ON</c> rejects any statement that writes;</description></item>
/// <item><description>statements are screened by <see cref="EnsureReadOnly"/>.</description></item>
/// </list>
/// Connections are not pooled so a lock on the file is released deterministically when the
/// connection is disposed.
/// </remarks>
public static class ReadOnlyConnection
{
    private static readonly HashSet<string> ForbiddenVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "INSERT", "UPDATE", "DELETE", "REPLACE", "DROP", "CREATE", "ALTER",
        "TRUNCATE", "VACUUM", "REINDEX", "ANALYZE", "ATTACH", "DETACH", "PRAGMA",
    };

    public static SqliteConnection Open(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        if (!File.Exists(databasePath))
            throw new FileNotFoundException("TONEX library not found.", databasePath);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            ForeignKeys = false,
        }.ToString();

        var connection = new SqliteConnection(connectionString);
        connection.Open();

        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA query_only = ON;";
            command.ExecuteNonQuery();
        }
        catch
        {
            connection.Dispose();
            throw;
        }

        return connection;
    }

    /// <summary>
    /// Throws when <paramref name="sql"/> could modify the database. Defence in depth: SQLite
    /// itself would refuse, but a clear exception localises the bug.
    /// </summary>
    public static void EnsureReadOnly(string sql)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sql);

        var scrubbed = StripComments(sql).TrimStart();
        var firstWord = ReadFirstWord(scrubbed);

        if (firstWord.Length == 0)
            return;

        if (ForbiddenVerbs.Contains(firstWord))
        {
            throw new InvalidOperationException(
                $"Refusing to run a statement that could modify a TONEX library: '{firstWord}...'.");
        }

        if (!firstWord.Equals("SELECT", StringComparison.OrdinalIgnoreCase)
            && !firstWord.Equals("WITH", StringComparison.OrdinalIgnoreCase)
            && !firstWord.Equals("EXPLAIN", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing to run a non-read statement against a TONEX library: '{firstWord}...'.");
        }
    }

    private static string ReadFirstWord(string sql)
    {
        var length = 0;
        while (length < sql.Length && char.IsLetter(sql[length]))
            length++;
        return sql[..length];
    }

    private static string StripComments(string sql)
    {
        var text = sql;

        var blockOpen = text.IndexOf("/*", StringComparison.Ordinal);
        if (blockOpen >= 0)
        {
            var blockClose = text.IndexOf("*/", blockOpen + 2, StringComparison.Ordinal);
            text = blockClose >= 0
                ? text.Remove(blockOpen, blockClose + 2 - blockOpen)
                : text[..blockOpen];
        }

        // A line comment only ends at the newline: deleting just that segment keeps any
        // statement that follows on the next line visible to the verb check.
        var lineComment = text.IndexOf("--", StringComparison.Ordinal);
        if (lineComment >= 0)
        {
            var endOfLine = text.IndexOf('\n', lineComment);
            text = endOfLine >= 0
                ? text.Remove(lineComment, endOfLine - lineComment)
                : text[..lineComment];
        }

        return text;
    }
}
