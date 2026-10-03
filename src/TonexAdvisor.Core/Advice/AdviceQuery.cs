namespace TonexAdvisor.Core.Advice;

/// <summary>
/// What the user is looking for: a song, an artist, a style — any combination of the three.
/// </summary>
/// <remarks>
/// The query is deliberately free text rather than a set of enum pickers: TONEX libraries carry
/// wildly inconsistent metadata (1 502 presets have <c>Genre=None</c>), so matching has to work
/// from whatever the user happens to know about the song.
/// </remarks>
public sealed record AdviceQuery
{
    public string Artist { get; init; } = "";

    public string Song { get; init; } = "";

    /// <summary>Free-form style: « metal », « blues saturé », « clean funk »…</summary>
    public string Style { get; init; } = "";

    /// <summary>True when the query carries no signal at all.</summary>
    public bool IsBlank =>
        string.IsNullOrWhiteSpace(Artist)
        && string.IsNullOrWhiteSpace(Song)
        && string.IsNullOrWhiteSpace(Style);

    public static AdviceQuery Create(string? artist, string? song, string? style) => new()
    {
        Artist = artist?.Trim() ?? "",
        Song = song?.Trim() ?? "",
        Style = style?.Trim() ?? "",
    };
}
