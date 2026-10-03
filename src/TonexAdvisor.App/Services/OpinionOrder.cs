using TonexAdvisor.App.Config;

namespace TonexAdvisor.App.Services;

/// <summary>
/// Ordre d'affichage des voix : la référence d'OpenCode d'abord, puis les challengeres dans
/// l'ordre du catalogue.
/// </summary>
/// <remarks>
/// Les avis arrivent dans l'ordre des réponses du réseau, qui change à chaque exécution : le
/// panneau, lui, doit être stable et toujours lire de la même façon — référence, voix, puis avis
/// convergé.
/// </remarks>
public static class OpinionOrder
{
    /// <summary>Plus petit = plus haut dans le panneau.</summary>
    public static int Rank(string provider)
    {
        if (provider.StartsWith("OpenCode", StringComparison.OrdinalIgnoreCase))
            return 0;

        for (var i = 0; i < AiProviders.Challengers.Count; i++)
        {
            if (string.Equals(AiProviders.Challengers[i].Label, provider, StringComparison.Ordinal))
                return i + 1;
        }

        return 99;
    }
}
