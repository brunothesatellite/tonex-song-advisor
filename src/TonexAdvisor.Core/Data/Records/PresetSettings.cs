using System.Globalization;

namespace TonexAdvisor.Core.Data.Records;

/// <summary>
/// The knob values of a V1 preset, split into numbers and free text so the UI can render
/// potentiometers and switches without knowing the underlying column types.
/// </summary>
/// <remarks>
/// Keys are the canonical TONEX column names (<c>ModelGain</c>, <c>EqBass</c>,
/// <c>VIRCabMic1X</c>, ...) so a recommendation produced by the AI can name a parameter the
/// user will recognise in TONEX itself.
/// </remarks>
public sealed class PresetSettings
{
    private readonly Dictionary<string, double> _numbers;
    private readonly Dictionary<string, string> _texts;

    public PresetSettings(
        IReadOnlyDictionary<string, double> numbers,
        IReadOnlyDictionary<string, string> texts,
        string origin = "")
    {
        ArgumentNullException.ThrowIfNull(numbers);
        ArgumentNullException.ThrowIfNull(texts);

        _numbers = new Dictionary<string, double>(numbers, StringComparer.OrdinalIgnoreCase);
        _texts = new Dictionary<string, string>(texts, StringComparer.OrdinalIgnoreCase);
        Origin = origin;
    }

    /// <summary>
    /// Where these values come from : the library itself (empty), or a library joined to it —
    /// a generation 2 library has none of its own.
    /// </summary>
    public string Origin { get; }

    public static PresetSettings Empty { get; } = new(
        new Dictionary<string, double>(),
        new Dictionary<string, string>());

    /// <summary>Numeric parameters (includes 0/1 switches).</summary>
    public IReadOnlyDictionary<string, double> Numbers => _numbers;

    /// <summary>Textual parameters.</summary>
    public IReadOnlyDictionary<string, string> Texts => _texts;

    /// <summary>Every parameter name present on this preset.</summary>
    public IEnumerable<string> Names => _numbers.Keys.Concat(_texts.Keys);

    public double? Number(string name)
        => _numbers.TryGetValue(name, out var value) ? value : null;

    public bool HasNumber(string name) => _numbers.ContainsKey(name);

    public string? Text(string name)
        => _texts.TryGetValue(name, out var value) ? value : null;

    /// <summary>Reads a 0/1 switch; missing parameters count as off.</summary>
    public bool IsEnabled(string name) => Number(name) is > 0;

    /// <summary>
    /// Formats a value for display: two decimals at most, invariant culture.
    /// </summary>
    public static string Format(double value)
        => value.ToString("0.##", CultureInfo.InvariantCulture);
}
