using Microsoft.Data.Sqlite;
using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Data.Readers;

/// <summary>Reads every tone model of a library.</summary>
public interface IToneModelReader
{
    IReadOnlyList<ToneModelRecord> ReadToneModels();
}

/// <summary>Reads every preset of a library.</summary>
public interface IPresetReader
{
    IReadOnlyList<PresetRecord> ReadPresets();
}

/// <summary>Both readers of one database generation, bound to the same connection.</summary>
public sealed class LibraryReaders
{
    public LibraryReaders(DatabaseFormat format, IPresetReader presets, IToneModelReader toneModels)
    {
        Format = format;
        Presets = presets;
        ToneModels = toneModels;
    }

    public DatabaseFormat Format { get; }

    public IPresetReader Presets { get; }

    public IToneModelReader ToneModels { get; }

    public static LibraryReaders Create(SqliteConnection connection, DatabaseFormat format)
        => format switch
        {
            DatabaseFormat.V1 => new LibraryReaders(
                format, new V1PresetReader(connection), new V1ToneModelReader(connection)),
            DatabaseFormat.V2 => new LibraryReaders(
                format, new V2PresetReader(connection), new V2ToneModelReader(connection)),
            _ => throw new NotSupportedException(
                $"Unsupported TONEX library format '{format}'."),
        };
}
