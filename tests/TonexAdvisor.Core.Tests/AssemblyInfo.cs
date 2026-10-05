using Xunit;

// La suite partage des singletons mondiaux : la culture du Localizer (bascule FR/EN des
// tests de localisation) et le dispatcher Avalonia. En parallèle, un test qui lit un texte
// affiché en FR pouvait croiser un test qui bascule la culture en EN — course observée de
// façon intermittente sur SettingsDatabaseChoiceTests (§18, phase 6). Sérialisation totale :
// le comportement des tests ne dépend plus de l'ordonnanceur.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
