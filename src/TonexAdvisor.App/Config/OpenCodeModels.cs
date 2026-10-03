namespace TonexAdvisor.App.Config;

/// <summary>Un modèle OpenCode Go proposé à l'utilisateur.</summary>
/// <param name="Id">Identifiant attendu par l'API <c>chat/completions</c>.</param>
/// <param name="Label">Libellé affiché dans la liste.</param>
public sealed record FreeModel(string Id, string Label);

/// <summary>
/// Modèles OpenCode Go <b>gratuits</b> (tarif « Free », quota illimité, offre limitée dans le temps).
/// </summary>
/// <remarks>
/// Source : <see href="https://opencode.ai/docs/go/#usage-limits">la table de tarification de Go</see>,
/// où seuls <c>LongCat 2.5 Preview Free</c> et <c>Space Bunny Free</c> sont facturés « Free ».
/// Tous les autres modèles de Go sont payants (consomment l'abonnement mensuel) : ils ne sont donc
/// jamais proposés ici. Les deux modèles ci-dessous ont été vérifiés avec la clé de l'utilisateur
/// (HTTP 200 sur <c>POST /zen/go/v1/chat/completions</c>).
/// </remarks>
public static class OpenCodeModels
{
    /// <summary>Base URL de l'offre OpenCode Go.</summary>
    public const string EndPoint = "https://opencode.ai/zen/go/v1";

    /// <summary>Les seuls modèles gratuits, à proposer à l'utilisateur.</summary>
    public static IReadOnlyList<FreeModel> Free { get; } = new[]
    {
        new FreeModel("longcat-2.5-preview-free", "LongCat 2.5 Preview Free"),
        new FreeModel("space-bunny-free", "Space Bunny Free"),
    };

    /// <summary>Modèle utilisé quand la configuration n'en précise aucun.</summary>
    public static string Default => Free[0].Id;

    /// <summary>Retourne le modèle demandé s'il fait partie de la liste gratuite, sinon le défaut.</summary>
    public static FreeModel Resolve(string? id)
        => Free.FirstOrDefault(model => string.Equals(model.Id, id, StringComparison.OrdinalIgnoreCase)) ?? Free[0];
}
