using System;
using System.Globalization;
using Avalonia.Data.Converters;

namespace TonexAdvisor.App.Localization;

/// <summary>
/// Séparation identité/affichage de la ComboBox de langue (§11, même discipline que §8.3) :
/// la valeur transportée par l'état est le code stable « fr »/« en », jamais le libellé —
/// comparer, persister et restaurer le choix restent possibles quelle que soit la langue
/// affichée. Seul le rendu passe par les clés <c>Reglages.Langue.Fr</c>/<c>.En</c>.
/// </summary>
public sealed class LanguageLabelConverter : IValueConverter
{
    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string code
            ? code switch
            {
                UiLanguages.French => Localizer.Instance["Reglages.Langue.Fr"],
                UiLanguages.English => Localizer.Instance["Reglages.Langue.En"],
                _ => code,
            }
            : value;

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Le choix de langue passe par le code, pas par l'affichage.");
}
