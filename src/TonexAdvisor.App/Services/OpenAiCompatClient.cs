using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TonexAdvisor.App.Services;

/// <summary>
/// One connecteur pour tous les fournisseurs qui exposent une API compatible OpenAI :
/// Gemini, Mistral et Groq.
/// </summary>
/// <remarks>
/// Seuls l'URL, la clé et le nom du modèle changent d'un fournisseur à l'autre ; le protocole est
/// identique (<c>POST /chat/completions</c>, <c>Bearer</c>, flux SSE). Aucun identifiant de
/// session n'est requis ici, contrairement à OpenCode Go.
/// </remarks>
public sealed class OpenAiCompatClient : IAiClient
{
    private static readonly HttpClient Http = CreateClient();

    private readonly string _endpoint;
    private readonly string _apiKey;

    public OpenAiCompatClient(string provider, string endpoint, string apiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        Provider = provider;
        _endpoint = endpoint.TrimEnd('/');
        _apiKey = apiKey;
    }

    public string Provider { get; }

    public async Task<string> AskStreamAsync(
        string model,
        string prompt,
        int maxTokens,
        Action<SseDelta> onDelta,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(onDelta);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);

        var payload = JsonSerializer.Serialize(new
        {
            model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = maxTokens,
            stream = true,
        });

        using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint + "/chat/completions")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await Http
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(DescribeError(response.StatusCode, errorBody));
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (!string.Equals(mediaType, "text/event-stream", StringComparison.OrdinalIgnoreCase))
        {
            // Pas de flux : le fournisseur renvoie la réponse complète.
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var text = ExtractText(body);
            if (text.Length > 0)
                onDelta(new SseDelta(text, false));
            return text;
        }

        var parser = new SseParser();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                break;

            var delta = parser.Feed(line);
            if (delta is not null)
                onDelta(delta);

            if (parser.IsDone)
                break;
        }

        return parser.Content.Length > 0 ? parser.Content : parser.Reasoning;
    }

    /// <summary>Les modèles réellement ouverts à cette clé (<c>GET /models</c>).</summary>
    /// <remarks>
    /// Les tarifs gratuits changent et un modèle finit par être retiré : plutôt que de deviner,
    /// on interroge le fournisseur et on montre ce qui est utilisable.
    /// </remarks>
    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, _endpoint + "/models");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(DescribeError(response.StatusCode, body));

        var ids = new List<string>();

        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("data", out var data)
                && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in data.EnumerateArray())
                {
                    if (entry.ValueKind == JsonValueKind.Object
                        && entry.TryGetProperty("id", out var id)
                        && id.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(id.GetString()))
                    {
                        ids.Add(id.GetString() ?? "");
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Liste illisible : on renverra simplement une liste vide.
        }

        return ids;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TonexAdvisor/1.0");
        return client;
    }

    private static string ExtractText(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            // Un corps qui n'est pas un objet JSON est légitime (message d'erreur brut) : il ne
            // doit jamais faire échouer l'extraction.
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return body.Trim();

            if (!document.RootElement.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array)
                return "";

            foreach (var choice in choices.EnumerateArray())
            {
                if (!choice.TryGetProperty("message", out var message))
                    continue;

                foreach (var property in new[] { "content", "reasoning_content", "text" })
                {
                    if (message.TryGetProperty(property, out var value)
                        && value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(value.GetString()))
                    {
                        return value.GetString() ?? "";
                    }
                }
            }
        }
        catch (JsonException)
        {
            // Corps illisible : pas de texte.
        }

        return "";
    }

    private static string DescribeError(System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            // Gemini renvoie parfois un corps qui n'est pas un objet : on lit alors le texte brut
            // plutôt que de planter et de masquer la vraie erreur.
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("error", out var error))
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
