namespace TonexAdvisor.App.Services;

/// <summary>Une erreur HTTP retournée par un fournisseur d'avis IA.</summary>
public sealed class AiRequestException : Exception
{
    public AiRequestException(int statusCode, string message)
        : base(message)
        => StatusCode = statusCode;

    /// <summary>Code HTTP du fournisseur.</summary>
    public int StatusCode { get; }

    /// <summary>
    /// Une panne qui peut passer toute seule : forte affluence (503), quota transitoire (429).
    /// Celles-là méritent un nouvel essai ; une clé invalide, non.
    /// </summary>
    public bool IsTransient => StatusCode == 429 || StatusCode >= 500;
}
