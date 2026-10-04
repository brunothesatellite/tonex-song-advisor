namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Série unique pour les tests de localisation (phase 8) : ils basculent la culture
/// GLOBALE du Localizer (setter = 4 cultures .NET + événements), donc ils
/// s'exécutent séquentiellement entre eux — pas d'entremêlement d'états partagés.
/// </summary>
[CollectionDefinition(LocalisationCollection.Nom)]
public sealed class LocalisationCollection
{
    /// <summary>Nom de la série xunit partagée.</summary>
    public const string Nom = "Localisation";
}
