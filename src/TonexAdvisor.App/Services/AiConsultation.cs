using System.Diagnostics;
using TonexAdvisor.App.Config;

namespace TonexAdvisor.App.Services;

/// <param name="Provider">Which voice.</param>
/// <param name="Text">What it answered, empty when it failed.</param>
/// <param name="Error">What went wrong, null when it answered.</param>
/// <param name="ElapsedMs">How long it took, useful to explain a timeout.</param>
public sealed record AiOpinion(string Provider, string Text, string? Error, long ElapsedMs)
{
    /// <summary>True when this voice contributed something usable.</summary>
    public bool Ok => Error is null && Text.Length > 0;
}

/// <summary>
/// Consults several AI voices on the same prompt, in parallel, and keeps going when one of them
/// fails.
/// </summary>
/// <remarks>
/// A panel is only useful if it survives a missing key, a rate limit or a slow provider: every
/// voice is asked independently, with its own deadline, and reports its own error. The caller
/// then decides what to do with what came back.
/// </remarks>
public static class AiConsultation
{
    /// <summary>
    /// The voices to consult: OpenCode first (the reference), then every challenger whose key
    /// the user filled in. No key, no voice.
    /// </summary>
    public static IReadOnlyList<IAiClient> BuildClients(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var clients = new List<IAiClient>();

        if (config.HasApiKey)
            clients.Add(new OpenCodeClient(config));

        foreach (var provider in AiProviders.Challengers)
        {
            var credential = config.CredentialFor(provider.Id);
            if (string.IsNullOrWhiteSpace(credential.ApiKey))
                continue;

            clients.Add(new OpenAiCompatClient(provider.Label, provider.EndPoint, credential.ApiKey));
        }

        return clients;
    }

    /// <summary>The model to use for a given voice, falling back to the catalogue.</summary>
    public static string ModelFor(AppConfig config, string providerLabel)
    {
        ArgumentNullException.ThrowIfNull(config);

        foreach (var provider in AiProviders.Challengers)
        {
            if (!string.Equals(provider.Label, providerLabel, StringComparison.Ordinal))
                continue;

            var model = config.CredentialFor(provider.Id).Model;
            return string.IsNullOrWhiteSpace(model) ? provider.DefaultModel : model;
        }

        return config.Model;
    }

    /// <summary>
    /// Asks every voice at once. A failure or a timeout is recorded as the opinion of that voice,
    /// never as an exception: the panel must render with whatever arrived.
    /// </summary>
    public static async Task<IReadOnlyList<AiOpinion>> ConsultAsync(
        IReadOnlyList<IAiClient> clients,
        Func<IAiClient, string> modelFor,
        string prompt,
        int maxTokens,
        TimeSpan timeout,
        Action<string, SseDelta>? onDelta = null,
        Action<AiOpinion>? onOpinion = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(modelFor);

        var tasks = clients
            .Select(client => AskOneAsync(
                client, modelFor(client), prompt, maxTokens, timeout, onDelta, onOpinion, cancellationToken))
            .ToList();

        var opinions = await Task.WhenAll(tasks).ConfigureAwait(false);
        return opinions;
    }

    private static async Task<AiOpinion> AskOneAsync(
        IAiClient client,
        string model,
        string prompt,
        int maxTokens,
        TimeSpan timeout,
        Action<string, SseDelta>? onDelta,
        Action<AiOpinion>? onOpinion,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);

        try
        {
            var text = await client.AskStreamAsync(
                model,
                prompt,
                maxTokens,
                delta => onDelta?.Invoke(client.Provider, delta),
                deadline.Token).ConfigureAwait(false);

            stopwatch.Stop();
            return Publish(new AiOpinion(client.Provider, text, null, stopwatch.ElapsedMilliseconds), onOpinion);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return Publish(new AiOpinion(
                client.Provider,
                "",
                $"Délai dépassé ({timeout.TotalSeconds:0} s)",
                stopwatch.ElapsedMilliseconds), onOpinion);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            return Publish(new AiOpinion(client.Provider, "", "Annulé", stopwatch.ElapsedMilliseconds), onOpinion);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return Publish(new AiOpinion(client.Provider, "", exception.Message, stopwatch.ElapsedMilliseconds), onOpinion);
        }
    }

    /// <summary>Reports an opinion as soon as it is ready, then returns it.</summary>
    private static AiOpinion Publish(AiOpinion opinion, Action<AiOpinion>? onOpinion)
    {
        onOpinion?.Invoke(opinion);
        return opinion;
    }
}
