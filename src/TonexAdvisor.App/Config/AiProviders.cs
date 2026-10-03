namespace TonexAdvisor.App.Config;

/// <summary>Une clé d'avis renseignée par l'utilisateur pour un fournisseur donné.</summary>
public sealed class ProviderCredential
{
    /// <summary>Clé API. Vide : le fournisseur ne participe pas aux avis.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Modèle demandé ; vide = le modèle gratuit par défaut du catalogue.</summary>
    public string Model { get; set; } = "";
}

/// <param name="Id">Identifiant stable, utilisé dans la configuration et l'IU.</param>
/// <param name="Label">Nom affiché.</param>
/// <param name="EndPoint">Base URL du service, compatible OpenAI (<c>…/chat/completions</c>).</param>
/// <param name="DefaultModel">Modèle gratuit proposé quand la configuration n'en précise pas.</param>
/// <param name="KeyUrl">Page officielle où demander une clé API.</param>
public sealed record AiProvider(
    string Id,
    string Label,
    string EndPoint,
    string DefaultModel,
    string KeyUrl);

/// <summary>
/// Les fournisseurs d'avis croisés proposés à l'utilisateur, tous gratuits avec une simple clé.
/// </summary>
/// <remarks>
/// Chacun expose une API compatible OpenAI, ce qui permet un seul connecteur pour les trois. Les
/// modèles par défaut sont ceux du gratuit : ils peuvent être remplacés dans
/// <c>config.json</c> si les tarifs changent.
/// </remarks>
public static class AiProviders
{
    /// <summary>Le fournisseur de référence, déjà branché dans les réglages.</summary>
    public const string OpenCodeId = "opencode";

    /// <summary>Les voix challengeres : l'utilisateur renseigne les clés qu'il souhaite.</summary>
    public static IReadOnlyList<AiProvider> Challengers { get; } =
    [
        new AiProvider(
            "gemini",
            "Gemini (Google)",
            "https://generativelanguage.googleapis.com/v1beta/openai",
            "gemini-2.5-flash",
            "https://aistudio.google.com/apikey"),

        new AiProvider(
            "mistral",
            "Mistral",
            "https://api.mistral.ai/v1",
            "mistral-small-latest",
            "https://console.mistral.ai/api-keys"),

        new AiProvider(
            "groq",
            "Groq",
            "https://api.groq.com/openai/v1",
            "llama-3.3-70b-versatile",
            "https://console.groq.com/keys"),
    ];

    /// <summary>Retrouve un fournisseur par son identifiant, indifféremment à la casse.</summary>
    public static AiProvider? Find(string? id)
        => Challengers.FirstOrDefault(provider =>
            !string.IsNullOrWhiteSpace(id)
            && string.Equals(provider.Id, id, StringComparison.OrdinalIgnoreCase));
}
