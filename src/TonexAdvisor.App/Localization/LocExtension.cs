using System;
using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace TonexAdvisor.App.Localization;

/// <summary>
/// Extension XAML <c>{loc:Loc Cle}</c> (§5) : retourne un <see cref="ReflectionBinding"/>
/// sur l'indexeur du <see cref="Localizer"/>. Le service notifiant <c>Item[]</c> à chaque
/// bascule de culture, tout texte branché se recharge sans redémarrage (§11).
/// </summary>
public sealed class LocExtension : MarkupExtension
{
    /// <param name="key">Clé de ressource, points inclus (ex. <c>Biblio.Recherche.Titre</c>).</param>
    public LocExtension(string key) => Key = key;

    /// <summary>Clé à afficher.</summary>
    public string Key { get; }

    /// <inheritdoc />
    public override object ProvideValue(IServiceProvider? serviceProvider)
        => new ReflectionBinding($"[{Key}]")
        {
            Source = Localizer.Instance,
            Mode = BindingMode.OneWay,
        };
}
