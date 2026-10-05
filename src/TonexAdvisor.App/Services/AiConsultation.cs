using System.Diagnostics;
using TonexAdvisor.App.Config;
using TonexAdvisor.App.Localization;

namespace TonexAdvisor.App.Services;

/// <summary>One voice of the panel: a client, the model it speaks with, and its display name.</summary>
/// <param name="Client">The API client to ask.</param>
/// <param name="Model">The model this voice speaks with — several voices may share one client.</param>
/// <param name="Label">What the panel shows: « OpenCode · longcat-… », « Gemini (Google) »…</param>
public sealed record Voice(IAiClient Client, string Model, string Label);

/// <param name="Provider">Which voice said it.</param>
/// <param name="Text">What it said, empty when it failed.</param>
/// <param name="Error">What went wrong, null when it answered.</param>
/// <param name="ElapsedMs">How long it took, useful to explain a timeout.</param>
public sealed record AiOpinion(string Provider, string Text, string? Error, long ElapsedMs)
{
    /// <summary>True when this voice contributed something usable.</summary>
    public bool Ok => Error is null && Text.Length > 0;
}

/// <summary>
/// Consults the voices in parallel, and keeps the arbitre apart from them.
/// </summary>
/// <remarks>
/// <para>
/// The panel has two roles, never mixed: the <b>voices</b> propose — every OpenCode model the
/// user ticked, plus the free voices he left on — and the <b>arbitre</b> decides between them.
/// With no voice at all, the arbitre speaks alone from the full prompt; with no arbitre (no
/// OpenCode key), the voices show as suggestions and no verdict is produced.
/// </para>
/// <para>
/// A voice that fails or times out becomes an opinion of its own, never an exception: the panel
/// must render with whatever arrived.
/// </para>
/// </remarks>
public static class AiConsultation
{
    /// <summary>
    /// Deadline for one voice. Generous on purpose: a reasoning model writes a long chain of
    /// thought before its first word, and cutting it at 60 s returned « délai dépassé » to voices
    /// that were seconds away from answering.
    /// </summary>
    public static readonly TimeSpan VoiceTimeout = TimeSpan.FromSeconds(180);

    /// <summary>
    /// The voices to consult: the OpenCode models the user ticked as voices, then the free
    /// voices whose key is filled in and whose switch is on.
    /// </summary>
    public static IReadOnlyList<Voice> BuildVoices(AppConfig config, UserState? state = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        var disabled = state?.DisabledVoices ?? new List<string>();
        var voices = new List<Voice>();

        // Un modèle OpenCode coché comme voix = une voix, dans l'ordre de la liste.
        if (config.HasApiKey)
        {
            var client = new OpenCodeClient(config);

            foreach (var model in config.VoiceModels)
            {
                if (string.IsNullOrWhiteSpace(model))
                    continue;

                voices.Add(new Voice(client, model, $"OpenCode · {model}"));
            }
        }

        foreach (var provider in AiProviders.Challengers)
        {
            var credential = config.CredentialFor(provider.Id);
            if (string.IsNullOrWhiteSpace(credential.ApiKey))
                continue;

            if (disabled.Any(id => string.Equals(id, provider.Id, StringComparison.OrdinalIgnoreCase)))
                continue;

            var model = string.IsNullOrWhiteSpace(credential.Model)
                ? provider.DefaultModel
                : credential.Model;

            voices.Add(new Voice(
                new OpenAiCompatClient(provider.Label, provider.EndPoint, credential.ApiKey),
                model,
                provider.Label));
        }

        return voices;
    }

    /// <summary>
    /// The arbitre: one OpenCode model, present only when the key is. It never appears among the
    /// voices — its job is to decide between them.
    /// </summary>
    public static Voice? BuildArbitre(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.HasApiKey
            ? new Voice(new OpenCodeClient(config), config.Model, config.Model)
            : null;
    }

    /// <summary>
    /// Asks every voice at once. A failure or a timeout is recorded as the opinion of that voice,
    /// never as an exception: the panel must render with whatever arrived.
    /// </summary>
    public static async Task<IReadOnlyList<AiOpinion>> ConsultAsync(
        IReadOnlyList<Voice> voices,
        string prompt,
        int maxTokens,
        Action<string, SseDelta>? onDelta = null,
        Action<AiOpinion>? onOpinion = null,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(voices);

        var deadline = timeout ?? VoiceTimeout;
        var tasks = voices
            .Select(voice => AskOneAsync(voice, prompt, maxTokens, deadline, onDelta, onOpinion, cancellationToken))
            .ToList();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    private static async Task<AiOpinion> AskOneAsync(
        Voice voice,
        string prompt,
        int maxTokens,
        TimeSpan timeout,
        Action<string, SseDelta>? onDelta,
        Action<AiOpinion>? onOpinion,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        // Une panne transitoire (forte affluence, quota) mérite un second essai ; une clé
        // invalide ou un modèle retiré, non.
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);

            try
            {
                var text = await voice.Client.AskStreamAsync(
                    voice.Model,
                    prompt,
                    maxTokens,
                    delta => onDelta?.Invoke(voice.Label, delta),
                    deadline.Token).ConfigureAwait(false);

                stopwatch.Stop();
                return Publish(new AiOpinion(voice.Label, text, null, stopwatch.ElapsedMilliseconds), onOpinion);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                return Publish(new AiOpinion(
                    voice.Label,
                    "",
                    Localizer.Instance.Get("Voix.Erreur.Delay", timeout.TotalSeconds),
                    stopwatch.ElapsedMilliseconds), onOpinion);
            }
            catch (OperationCanceledException)
            {
                stopwatch.Stop();
                return Publish(new AiOpinion(
                    voice.Label, "", Localizer.Instance["Voix.Erreur.Annule"], stopwatch.ElapsedMilliseconds),
                    onOpinion);
            }
            catch (AiRequestException exception) when (exception.IsTransient && attempt == 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                stopwatch.Stop();
                return Publish(new AiOpinion(voice.Label, "", exception.Message, stopwatch.ElapsedMilliseconds), onOpinion);
            }
        }

        stopwatch.Stop();
        return Publish(
            new AiOpinion(voice.Label, "", Localizer.Instance["Voix.Erreur.Retry"], stopwatch.ElapsedMilliseconds),
            onOpinion);
    }

    /// <summary>Reports an opinion as soon as it is ready, then returns it.</summary>
    private static AiOpinion Publish(AiOpinion opinion, Action<AiOpinion>? onOpinion)
    {
        onOpinion?.Invoke(opinion);
        return opinion;
    }
}
