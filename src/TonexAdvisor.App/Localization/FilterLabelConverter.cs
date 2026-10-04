using System;
using System.Globalization;
using Avalonia.Data.Converters;
using TonexAdvisor.App.ViewModels;

namespace TonexAdvisor.App.Localization;

/// <summary>
/// Séparation identité/étiquette de la sentinelle de filtre (§8.3, piège n°2) : la valeur
/// « Tous » portée par l'état (<see cref="LibraryViewModel.AnyFilter"/>) ne bouge jamais —
/// comparaisons et restauration de <c>state.json</c> sont préservées (G8) ; seul l'affichage
/// dans la ComboBox passe par la clé <c>Filtre.Tous</c>. Les autres valeurs (catégories,
/// genres, dossiers réels) sont de la donnée utilisateur : affichées telles quelles.
/// </summary>
public sealed class FilterLabelConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string texte
            && string.Equals(texte, LibraryViewModel.AnyFilter, StringComparison.Ordinal)
                ? Localizer.Instance["Filtre.Tous"]
                : value;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("L'affichage des filtres ne se convertit pas en retour.");
}
