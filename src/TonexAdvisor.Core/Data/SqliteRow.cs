using System.Globalization;
using Microsoft.Data.Sqlite;

namespace TonexAdvisor.Core.Data;

/// <summary>
/// Tolerant accessor over a single result row.
/// </summary>
/// <remarks>
/// TONEX ships schema revisions under the same file name, so columns come and go between
/// releases. Every accessor therefore resolves by name and degrades to a default instead of
/// throwing, which keeps a slightly older or newer library readable.
/// </remarks>
public readonly struct SqliteRow
{
    private readonly SqliteDataReader _reader;
    private readonly Dictionary<string, int>? _index;

    public SqliteRow(SqliteDataReader reader, Dictionary<string, int>? index)
    {
        _reader = reader;
        _index = index;
    }

    /// <summary>Returns the zero-based ordinal of <paramref name="column"/>, or -1 when absent.</summary>
    public int OrdinalOf(string column)
    {
        if (_index is null)
            return -1;
        return _index.TryGetValue(column, out var ordinal) ? ordinal : -1;
    }

    public bool Has(string column) => OrdinalOf(column) >= 0;

    public object? Raw(string column)
    {
        var ordinal = OrdinalOf(column);
        if (ordinal < 0 || _reader.IsDBNull(ordinal))
            return null;
        return _reader.GetValue(ordinal);
    }

    public string String(string column, string fallback = "")
    {
        var value = Raw(column);
        return value switch
        {
            null => fallback,
            string text => text,
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? fallback,
        };
    }

    public double? Number(string column)
    {
        var value = Raw(column);
        return value switch
        {
            null => null,
            double d => d,
            float f => f,
            int i => i,
            long l => l,
            decimal m => (double)m,
            bool b => b ? 1d : 0d,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            string text when long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    public int Int(string column, int fallback = 0) => (int)Math.Round(Number(column) ?? fallback);

    public bool Flag(string column, bool fallback = false)
    {
        var number = Number(column);
        return number is null ? fallback : Math.Abs(number.Value) > double.Epsilon;
    }

    public Guid ReadGuid(string column)
    {
        var text = String(column);
        return System.Guid.TryParse(text, out var parsed) ? parsed : System.Guid.Empty;
    }
}
