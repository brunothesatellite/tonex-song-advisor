using System.Text.Json;

namespace TonexAdvisor.App.Services;

/// <param name="Text">The fragment itself.</param>
/// <param name="IsReasoning">
/// True for the chain of thought, false for the final answer. Reasoning models send the first
/// one before the second, and sometimes nothing else.
/// </param>
public sealed record SseDelta(string Text, bool IsReasoning);

/// <summary>
/// Minimal reader for the server-sent events of <c>chat/completions</c>.
/// </summary>
/// <remarks>
/// Kept separate from <see cref="OpenCodeClient"/> so the stream handling can be tested without a
/// network: every line is fed to <see cref="Feed"/>, which accumulates the two text streams and
/// reports each fragment as it arrives.
/// </remarks>
public sealed class SseParser
{
    /// <summary>Final answer accumulated so far.</summary>
    public string Content { get; private set; } = "";

    /// <summary>Chain of thought accumulated so far.</summary>
    public string Reasoning { get; private set; } = "";

    /// <summary>True once the service announced the end of the stream.</summary>
    public bool IsDone { get; private set; }

    /// <summary>
    /// Feeds one stream line. Returns the fragment it carried, or null for keep-alives,
    /// comments, partial lines and anything not worth showing.
    /// </summary>
    public SseDelta? Feed(string? line)
    {
        if (string.IsNullOrEmpty(line))
            return null;

        // Keep-alives and comments.
        if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            return null;

        var payload = line[5..].Trim();
        if (payload.Length == 0)
            return null;

        if (payload == "[DONE]")
        {
            IsDone = true;
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);

            if (!document.RootElement.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var choice in choices.EnumerateArray())
            {
                // Two shapes are seen in the wild: `choices[].delta` while streaming and
                // `choices[].message` for the last event of some providers.
                if (choice.TryGetProperty("delta", out var streamed) && streamed.ValueKind == JsonValueKind.Object)
                {
                    var delta = Extract(streamed);
                    if (delta is not null)
                        return delta;
                }

                if (choice.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object)
                {
                    var delta = Extract(message);
                    if (delta is not null)
                        return delta;
                }
            }
        }
        catch (JsonException)
        {
            // A line cut in the middle of a fragment: the next one carries the rest.
        }

        return null;
    }

    /// <summary>
    /// Takes the first piece of text a payload carries: the answer first, then the chain of
    /// thought, then the plain <c>text</c> field some providers use instead of <c>content</c>.
    /// </summary>
    private SseDelta? Extract(JsonElement payload)
    {
        foreach (var property in new[] { "content", "reasoning_content", "text" })
        {
            if (!payload.TryGetProperty(property, out var value))
                continue;
            if (value.ValueKind != JsonValueKind.String)
                continue;

            var text = value.GetString() ?? "";
            if (text.Length == 0)
                continue;

            var isReasoning = property == "reasoning_content";
            if (isReasoning)
                Reasoning += text;
            else
                Content += text;

            return new SseDelta(text, isReasoning);
        }

        return null;
    }
}
