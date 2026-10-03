using Microsoft.Data.Sqlite;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Data.Readers;

/// <summary>
/// Reads <c>Library.db</c> presets, where every knob is a column of the <c>Presets</c> table.
/// </summary>
public sealed class V1PresetReader : IPresetReader
{
    /// <summary>
    /// Column name prefixes that identify a knob rather than metadata. Tags, keys and flags are
    /// handled explicitly by <see cref="Map"/>.
    /// </summary>
    private static readonly string[] ParameterPrefixes =
    {
        "Model", "PwrAmpEq", "Eq", "Comp", "NoiseGate", "BPM", "Mod", "Delay",
        "Reverb", "Cab", "VIRCab", "HWParam", "HW_",
    };

    private static readonly string[] ExcludedColumns =
    {
        "GUID", "Version", "ToneModel_GUID", "OptionalToneModel_GUID", "Favorite", "DateTimeAdded",
    };

    private readonly SqliteConnection _connection;

    public V1PresetReader(SqliteConnection connection)
        => _connection = connection ?? throw new ArgumentNullException(nameof(connection));

    public IReadOnlyList<PresetRecord> ReadPresets()
    {
        var parameterColumns = SchemaSniffer.ColumnsOf(_connection, "Presets")
            .Where(IsParameter)
            .OrderBy(column => column, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var folders = LibraryLookups.PresetFolders(_connection);

        return SqliteQuery.Select(
            _connection,
            "SELECT * FROM Presets",
            row => Map(row, parameterColumns, folders));
    }

    private static bool IsParameter(string column)
    {
        if (ExcludedColumns.Contains(column, StringComparer.OrdinalIgnoreCase))
            return false;
        if (column.StartsWith("Tag_", StringComparison.OrdinalIgnoreCase))
            return false;

        return ParameterPrefixes.Any(prefix => column.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static PresetRecord Map(
        SqliteRow row,
        IReadOnlyList<string> parameterColumns,
        IReadOnlyDictionary<string, List<string>> folders)
    {
        var name = row.String("Tag_PresetName").Trim();

        var toneModelKeys = new List<string>(2);
        var primary = row.String("ToneModel_GUID").Trim();
        if (primary.Length > 0)
            toneModelKeys.Add(primary);

        // The second slot exists so a preset can swap to an alternative capture; it is only
        // relevant to the user if it actually points somewhere.
        var optional = row.String("OptionalToneModel_GUID").Trim();
        if (optional.Length > 0 && !toneModelKeys.Contains(optional, StringComparer.OrdinalIgnoreCase))
            toneModelKeys.Add(optional);

        return new PresetRecord
        {
            Key = row.String("GUID").Trim(),
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
            ToneModelKeys = toneModelKeys,
            Chain = Array.Empty<ChainBlock>(),
            Settings = ReadSettings(row, parameterColumns),
        };
    }

    private static PresetSettings ReadSettings(SqliteRow row, IReadOnlyList<string> parameterColumns)
    {
        var numbers = new Dictionary<string, double>(parameterColumns.Count, StringComparer.OrdinalIgnoreCase);
        var texts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in parameterColumns)
        {
            var number = row.Number(column);
            if (number.HasValue)
            {
                numbers[column] = number.Value;
                continue;
            }

            var text = row.String(column);
            if (text.Length > 0)
                texts[column] = text;
        }

        return new PresetSettings(numbers, texts);
    }
}
