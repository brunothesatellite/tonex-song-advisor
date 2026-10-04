using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Data;
using Avalonia.Data.Core;
using Avalonia.Threading;
using TonexAdvisor.App.Localization;

namespace TonexAdvisor.Core.Tests;

/// <summary>
/// Tests de l'extension <c>{loc:Loc}</c> (phase 8, §5) : elle doit produire un binding
/// vivant sur l'indexeur du Localizer — la valeur suit les bascules de culture, ce qui
/// garantit l'UI « migrée d'un coup » au lancement de la phase 3.
/// </summary>
[Collection(LocalisationCollection.Nom)]
public class LocExtensionTests
{
    /// <summary>Cible de property styled minimaliste, sans dépendance visuelle.</summary>
    private sealed class Cible : AvaloniaObject
    {
        public static readonly StyledProperty<string?> ValeurProperty =
            AvaloniaProperty.Register<Cible, string?>(nameof(Valeur));

        public string? Valeur
        {
            get => GetValue(ValeurProperty);
            set => SetValue(ValeurProperty, value);
        }
    }

    /// <summary>
    /// Valeur courante de l'expression de binding. La lecture de la cible démarre
    /// l'expression si besoin ; <c>GetValue()</c> est appelé par réflexion car
    /// l'assembly de référence d'Avalonia 12.1.3 ne l'expose pas au compile-time
    /// alors qu'il est public en runtime (constat phase 1).
    /// La valeur est posée de façon synchrone par <c>PublishValue</c>, indépendamment
    /// de la poussée vers la cible (celle-ci passe par <c>Dispatcher.UIThread</c> —
    /// non déterministe en suite headless, validée visuellement en phase 3).
    /// </summary>
    private static object? ValeurDe(Cible cible, UntypedBindingExpressionBase expression)
    {
        _ = cible.Valeur;

        var getValue = expression.GetType().GetMethod("GetValue", Type.EmptyTypes);
        Assert.NotNull(getValue);

        try
        {
            return getValue!.Invoke(expression, null);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw exception.InnerException;
        }
    }

    /// <summary>
    /// Réinitialise l'état global du dispatcher et réclame sa possession pour le fil du
    /// test, avec retries (fenêtre de course avec les tests parallèles qui y touchent).
    /// En suite complète, d'autres tests ont pu créer le dispatcher UI en premier : notre
    /// fil n'en serait plus propriétaire et l'abonnement INPC basculerait en mode « posté »
    /// (WeakEvents.ThreadSafePropertyChanged → Dispatcher.UIThread.Post), jamais exécuté
    /// ici — la bascule de culture ne se propagerait plus.
    /// API interne d'Avalonia prévue pour ses tests unitaires : ResetBeforeUnitTests =
    /// ResetGlobalState (sans fermeture de boucle, contrairement à ResetForUnitTests qui
    /// tuerait le dispatcher partagé sous les tests parallèles).
    /// </summary>
    private static void ReclamerLeDispatcher()
    {
        var reset = typeof(Dispatcher).GetMethod(
            "ResetBeforeUnitTests", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(reset);

        for (var essai = 0; essai < 10; essai++)
        {
            reset!.Invoke(null, null);
            _ = Dispatcher.UIThread; // le premier créateur devient propriétaire (s_uiThread ??= this)

            if (Dispatcher.UIThread.CheckAccess())
                return;
        }

        Assert.Fail("Impossible d'obtenir la possession du dispatcher UI après 10 essais.");
    }

    [Fact]
    public void Fournit_un_binding_vivant_sur_l_indexeur()
    {
        using var garde = new CultureGuard();
        ReclamerLeDispatcher();

        var binding = Assert.IsType<ReflectionBinding>(new LocExtension("Filtre.Tous").ProvideValue(null!));
        Assert.Equal("[Filtre.Tous]", binding.Path);
        Assert.Same(Localizer.Instance, binding.Source);

        var cible = new Cible();
        var expression = Assert.IsAssignableFrom<UntypedBindingExpressionBase>(
            cible.Bind(Cible.ValeurProperty, binding));

        Assert.Equal("Tous", ValeurDe(cible, expression));

        var recues = new List<string?>();
        PropertyChangedEventHandler probe = (_, e) => recues.Add(e.PropertyName);
        Localizer.Instance.PropertyChanged += probe;
        Localizer.Instance.Culture = CultureInfo.GetCultureInfo("en-US");
        Localizer.Instance.PropertyChanged -= probe;

        var direct = Localizer.Instance["Filtre.Tous"];
        var expr = ValeurDe(cible, expression) as string;

        if (!string.Equals(expr, "All", StringComparison.Ordinal))
        {
            Assert.Fail($"direct={direct}; expr={expr}; evenements=[{string.Join("|", recues)}]; " +
                        $"uiOwner={Dispatcher.UIThread.CheckAccess()}");
        }

        Assert.Equal("All", expr);

        Localizer.Instance.Culture = CultureInfo.GetCultureInfo("fr-FR");
        Assert.Equal("Tous", ValeurDe(cible, expression));
    }

    [Fact]
    public void Nom_de_cle_est_conserve_quelles_que_soient_les_options()
    {
        var extension = new LocExtension("Marqueur.Bloc");

        Assert.Equal("Marqueur.Bloc", extension.Key);
    }
}
