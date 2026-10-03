using System.Text.Json;

namespace TonexAdvisor.Core.Data.Records;

/// <summary>One slot of a V2 preset signal chain.</summary>
/// <param name="Id">
/// Numeric block identifier. The mapping from identifier to effect module is not documented;
/// the values observed in real libraries are -1 (empty slot) and 0-32.
/// </param>
/// <param name="Bypass">True when the block exists in the chain but is switched off.</param>
public readonly record struct ChainBlock(int Id, bool Bypass)
{
    /// <summary>True for the padding entries TONEX writes to fill unused chain slots.</summary>
    public bool IsEmpty => Id < 0;

    /// <summary>Parses the JSON stored in <c>Presets.Chain</c>.</summary>
    /// <param name="json">Raw column value; null, empty or malformed input yields an empty chain.</param>
    public static IReadOnlyList<ChainBlock> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<ChainBlock>();

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return Array.Empty<ChainBlock>();

            var blocks = new List<ChainBlock>();
            foreach (var element in document.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object)
                    continue;

                var id = element.TryGetProperty("ID", out var idElement) && idElement.TryGetInt32(out var parsedId)
                    ? parsedId
                    : -1;

                var bypass = element.TryGetProperty("Bypass", out var bypassElement)
                    && bypassElement.ValueKind == JsonValueKind.True;

                blocks.Add(new ChainBlock(id, bypass));
            }

            return blocks;
        }
        catch (JsonException)
        {
            return Array.Empty<ChainBlock>();
        }
    }
}
