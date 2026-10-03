using System.Text;
using System.Text.Json;
using TonexAdvisor.App.Config;

namespace TonexAdvisor.App.Services;

/// <summary>
/// Client HTTP minimal pour l'API <b>OpenCode Go</b> (<c>chat/completions</c> OpenAI-compatible).
/// </summary>
/// <remarks>
/// L'exigence du service : un <c>User-Agent</c> identifiant le client et un identifiant de session
/// stable envoyé dans <c>x-opencode-session</c> — sans ce dernier, l'API répond
/// <c>MissingSessionID</c>. Un identifiant est donc généré par instance et réutilisé pour toutes
/// les requêtes de la session de discussion.
/// </remarks>
public sealed class OpenCodeClient
{
    private static readonly HttpClient Http = CreateClient();

    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _sessionId = Guid.NewGuid().ToString();

    public OpenCodeClient(AppConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        _apiKey = config.ApiKey;
        _endpoint = (config.Endpoint ?? OpenCodeModels.EndPoint).TrimEnd('/');
    }

    /// <summary>Identifiant de session envoyé à chaque requête.</summary>
    public string SessionId => _sessionId;

    /// <summary>Ping de connectivité : vérifie la clé et le modèle en une seule requête.</summary>
    public Task<string> PingAsync(string model, CancellationToken cancellationToken = default)
        // Généreux en tokens : les modèles de raisonnement sortent d'abord leur chaîne de pensée
        // et n'arrivent au texte final qu'après (sinon la réponse « vide » n'est que du raisonnement).
        => AskAsync(model, "Réponds uniquement par le mot OK.", maxTokens: 1000, cancellationToken);

    /// <summary>Envoie un prompt et renvoie le texte de la réponse.</summary>
    public async Task<string> AskAsync(
        string model,
        string prompt,
        int maxTokens = 700,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Aucune clé API OpenCode enregistrée (réglages → IA).");

        var payload = JsonSerializer.Serialize(new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = maxTokens,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint + "/chat/completions")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        request.Headers.TryAddWithoutValidation("x-opencode-session", _sessionId);

        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(DescribeError(response.StatusCode, body));

        return ExtractText(body);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TonexAdvisor/1.0");
        return client;
    }

    /// <summary>Récupère <c>choices[0].message.content</c>, en retombant sur le raisonnement.</summary>
    private static string ExtractText(string body)
    {
        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
            return "";

        foreach (var choice in choices.EnumerateArray())
        {
            if (!choice.TryGetProperty("message", out var message))
                continue;

            foreach (var property in new[] { "content", "reasoning_content" })
            {
                if (message.TryGetProperty(property, out var value)
                    && value.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(value.GetString()))
                {
                    return value.GetString() ?? "";
                }
            }
        }

        return "";
    }

    /// <summary>Transforme la erreur JSON du service en message lisible.</summary>
    private static string DescribeError(System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message))
                    return $"Erreur IA ({(int)status}) : {message.GetString()}";
                if (error.ValueKind == JsonValueKind.String)
                    return $"Erreur IA ({(int)status}) : {error.GetString()}";
            }
        }
        catch (JsonException)
        {
            // On retombe sur le corps brut ci-dessous.
        }

        var text = body.Trim();
        return $"Erreur IA ({(int)status}) : {(text.Length > 300 ? text[..300] : text)}";
    }
}
