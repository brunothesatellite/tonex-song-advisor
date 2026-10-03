using Microsoft.Data.Sqlite;
using TonexAdvisor.Core.Data.Readers;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Data;

/// <summary>
/// An open, strictly read-only view of a TONEX library database.
/// </summary>
/// <remarks>
/// Opening captures the file's SHA-256 so <see cref="VerifyUnchanged"/> can prove afterwards
/// that the session never modified the user's library.
/// </remarks>
public sealed class ToneXDatabase : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LibraryReaders _readers;
    private bool _disposed;

    private ToneXDatabase(
        string path,
        string sha256,
        DatabaseFormat format,
        IReadOnlyList<string> tables,
        SqliteConnection connection,
        LibraryReaders readers)
    {
        Path = path;
        Sha256 = sha256;
        Format = format;
        Tables = tables;
        _connection = connection;
        _readers = readers;
    }

    /// <summary>Absolute path of the database file.</summary>
    public string Path { get; }

    /// <summary>Detected generation.</summary>
    public DatabaseFormat Format { get; }

    /// <summary>User tables present in the file.</summary>
    public IReadOnlyList<string> Tables { get; }

    /// <summary>SHA-256 of the file captured when it was opened.</summary>
    public string Sha256 { get; }

    /// <summary>True when the library carries numeric knob values (generation 1).</summary>
    public bool HasKnobSettings => Format == DatabaseFormat.V1;

    /// <summary>Opens <paramref name="path"/> read-only and detects its format.</summary>
    public static ToneXDatabase Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var sha256 = FileIntegrity.Sha256(path);
        var connection = ReadOnlyConnection.Open(path);

        try
        {
            var format = SchemaSniffer.Detect(connection);
            var tables = SchemaSniffer.TablesOf(connection);
            var readers = LibraryReaders.Create(connection, format);
            return new ToneXDatabase(path, sha256, format, tables, connection, readers);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>Reads every preset.</summary>
    public IReadOnlyList<PresetRecord> LoadPresets()
    {
        EnsureNotDisposed();
        return _readers.Presets.ReadPresets();
    }

    /// <summary>Reads every tone model.</summary>
    public IReadOnlyList<ToneModelRecord> LoadToneModels()
    {
        EnsureNotDisposed();
        return _readers.ToneModels.ReadToneModels();
    }

    /// <summary>Count of rows in a table, or 0 when the table is absent.</summary>
    public long Count(string table)
    {
        EnsureNotDisposed();

        var quoted = table.Replace("\"", "\"\"", StringComparison.Ordinal);
        using var command = _connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM \"{quoted}\"";
        try
        {
            return Convert.ToInt64(command.ExecuteScalar(), System.Globalization.CultureInfo.InvariantCulture);
        }
        catch (SqliteException)
        {
            return 0;
        }
    }

    /// <summary>
    /// Recomputes the file hash. Returns true when it matches the value captured at open time.
    /// </summary>
    public bool VerifyUnchanged()
    {
        EnsureNotDisposed();
        return string.Equals(FileIntegrity.Sha256(Path), Sha256, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _connection.Dispose();
    }

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
