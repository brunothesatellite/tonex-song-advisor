using TonexAdvisor.Core.Data.Records;

namespace TonexAdvisor.Core.Data;

/// <summary>The observed value span of a single preset parameter.</summary>
public readonly record struct ParameterRange(string Param, double Min, double Max)
{
    /// <summary>Span width; a constant parameter collapses to 0.</summary>
    public double Span => Max - Min;

    /// <summary>Maps a value onto 0..1, clamped. Constant parameters report 0.5.</summary>
    public double Normalize(double value)
    {
        if (Span <= double.Epsilon)
            return 0.5;

        var ratio = (value - Min) / Span;
        return Math.Clamp(ratio, 0d, 1d);
    }
}

/// <summary>
/// Computes the min/max of every preset parameter across a library, which is what turns raw
/// column values into potentiometer positions.
/// </summary>
/// <remarks>
/// TONEX does not document its parameter ranges and they differ between blocks (gain is 0..10,
/// mix is 0..100), so the real span observed in the user's own library is the only trustworthy
/// basis for drawing a knob.
/// </remarks>
public static class ParameterRangeCalculator
{
    public static IReadOnlyDictionary<string, ParameterRange> Calculate(IEnumerable<PresetRecord> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);

        var minimums = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var maximums = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);

        foreach (var preset in presets)
        {
            if (preset.Settings is null)
                continue;

            foreach (var (name, value) in preset.Settings.Numbers)
            {
                if (minimums.TryGetValue(name, out var currentMin))
                    minimums[name] = Math.Min(currentMin, value);
                else
                    minimums[name] = value;

                if (maximums.TryGetValue(name, out var currentMax))
                    maximums[name] = Math.Max(currentMax, value);
                else
                    maximums[name] = value;
            }
        }

        var ranges = new Dictionary<string, ParameterRange>(StringComparer.OrdinalIgnoreCase);
        foreach (var (name, min) in minimums)
            ranges[name] = new ParameterRange(name, min, maximums[name]);

        return ranges;
    }
}
