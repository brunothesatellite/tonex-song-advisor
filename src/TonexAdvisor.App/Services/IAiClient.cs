namespace TonexAdvisor.App.Services;

/// <summary>
/// A voice in the advice panel: anything that can answer a prompt, whatever its provider.
/// </summary>
/// <remarks>
/// The consultation layer only knows this interface, so OpenCode, Gemini, Mistral and Groq are
/// interchangeable and a provider that fails or times out costs one opinion, not the advice.
/// </remarks>
public interface IAiClient
{
    /// <summary>Display name, used in the crossed opinions and in the status lines.</summary>
    string Provider { get; }

    /// <summary>Sends a prompt and streams the answer back.</summary>
    Task<string> AskStreamAsync(
        string model,
        string prompt,
        int maxTokens,
        Action<SseDelta> onDelta,
        CancellationToken cancellationToken = default);
}
