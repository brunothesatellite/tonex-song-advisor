# INTERNATIONALISATION — Plan Phase 8 (français / anglais)

> **Statut** : plan de conception validé, **aucun code écrit** — branche
> `evol-internationalisation`. Décisions de l'utilisateur actées le 04/10/2026 : voir §17.
> Objectif : l'application en **français ou en anglais**, détection au premier démarrage, choix dans
> les réglages, bascule **sans redémarrage**, architecture ouverte sur une troisième langue —
> avec une **non-régression garantie et démontrable** à chaque étape.
>
> Règle de travail inchangée : **rien n'est commité sans instruction explicite.**

---

## 1. Objectifs et hors-périmètre

**Objectifs**

- FR = langue de référence, **identique caractère à caractère** à l'UI actuelle (extraction
  verbatim, aucun reformatement) → les 195 tests actuels restent verts **sans être modifiés**.
- EN complet dès la livraison (parité de clés vérifiée par test, pas par promesse).
- Bascule de langue **à chaud**, sans redémarrage, y compris les chaînes déjà calculées
  (messages d'état, titres d'avis, récapitulables de compteurs).
- Architecture prête pour une 3ᵉ langue : ajouter un fichier de ressources + une règle de
  pluriel, **sans toucher aux écrans**.
- **Installeur en français et en anglais** (décision 2) et **README anglais** (décision 4,
  livré immédiatement avec ce plan).

**Hors-périmètre (volontaire)**

- RTL / arabe / hébreu : pas de retournement de mise en page, fr et en étant LTR.
- Traduction des données (noms de presets, catégories `HI-GAIN`, dossiers, noms de modèles) :
  ce sont des **données utilisateur**, pas de l'UI.
- Chaînes **développeur** : journaux, messages d'exception brutes, commentaires, `ViewLocator`
  « Not Found » (hors écran utilisateur réel).
- Captures d'écran : non refaites en anglais avant une release ultérieure (le corps du README
  est traduit, les images restent les mêmes).

---

## 2. Inventaire mesuré (état des lieux réel, aujourd'hui)

Mesures faites sur le dépôt au commit `375e146` (v1.2.1), méthodes reproductibles indiquées.

| Catégorie | Mesure | Où |
|---|---|---|
| Valeurs littérales dans le XAML (Text, Header, Content, ToolTip, Watermark) | **116** (Library 39, Settings 36, MainWindow 14, ToneModelDetail 14, PresetDetail 13) | `src/TonexAdvisor.App/Views/*.axaml` |
| Lignes de C# contenant du texte français accentué | **~75** (SettingsViewModel 32, AdviceViewModel 16, PresetDetailVM 8, MainViewModel 6, autres ~13) | `ViewModels/`, `Services/`, `Config/` |
| Literales de chaînes au total dans l'App (code + UI mélangées, bornes hautes) | **~315** → sous-ensemble utilisateur estimé **150 à 200** | `src/TonexAdvisor.App/**/*.cs` |
| Appels de formatage (ToString formatés, `string.Format`, `DateTime`) | **33** | majoritairement LibraryVM (9), SettingsVM (6), AnswerLineVM (5) |
| Tests assertant du texte français | **16** dans 8 fichiers | voir §10 |
| Chaînes IA (invites) | 2 consignes de langue + tout le format de sortie | `Core/Advice/CataloguePrompt.cs:57`, `ArbitrationPrompt.cs:38` |
| Chaînes **Core** visibles à l'écran (raisons du conseil type « Favori de la bibliothèque », libellés de potards, note de jointure V1) | **~30 à 40** | `LibraryAdvisor.cs` (19 lignes accentuées), `KnobCatalog.cs` (9), `SettingsJoin.cs` (1) |
| Marqueurs de protocole | 3 couches distinctes (invite, extraction, coloration) | voir §9 |
| Compteurs/composés dynamiques | `:N0` + phrase, ex. `$"{n:N0} preset(s) à {m:N0} tone model(s)"` | `MainViewModel.cs:115-120`, `LibraryViewModel.cs:362` |
| Infra i18n existante | **aucune** (0 `CultureInfo`, 0 `.resx`, 0 `Localiz*`) | — |
| Aide-mémoire tramage publish | publish **sans trimming** → `ResourceManager` sûr | `tools/build-release.ps1:28-29` |

L'estimation honnête d'ouvrage : **~280 à 350 clés** (XAML + ViewModels App + raisons et
libellés Core + invites + marqueurs), contre « 150 à 250 » annoncé dans le TODO — l'écart vient
des chaînes **sans accent** (« Tous », « preset(s) », libellés courts) non comptées par la
méthode des accents.

---

## 3. Pièges identifiés dans le code (les vrais risques de non-régression)

Ces sept points sont des **pieges avérés**, mesurés dans le code, pas des hypothèses. Chacun a sa
parade dans le plan.

1. **Largeurs de colonnes clés par l'en-tête affiché.**
   `LibraryView.axaml.cs:126-135` applique `widths[header]` en comparant
   `column.Header?.ToString()` avec `StringComparison.Ordinal`, et `:218-225` enregistre sous la
   clé = en-tête. Si les en-têtes deviennent anglais, les largeurs **sauvegardées en français ne
   retrouvent plus leur colonne** → les réglages de l'utilisateur v1.2.1 sont perdus au premier
   passage en EN. → §8.2 : migration vers des **identifiants de colonnes stables** avec
   rétrocompatibilité.

2. **Valeur sentinelle « Tous » mêlée à l'affichage.**
   `LibraryViewModel.cs:15` `AnyFilter = "Tous"` sert à la fois d'identité logique
   (`SelectedCategory == AnyFilter`, `:332-334`) **et** d'item affiché dans les ComboBox. Si on
   traduit la constante, les comparaisons et la restauration d'état cassent ; si on la laisse et
   qu'on l'affiche telle quelle, l'EN voit « Tous ». → §8.3 : **la sentinelle ne bouge pas, seul
   l'affichage se localise** (séparateur identité/étiquette).

3. **Marqueurs de protocole IA présents en trois couches.**
   - invite (ce que le modèle doit écrire) : `CataloguePrompt.cs:162-173`, `ArbitrationPrompt.cs:68-84` ;
   - extraction (ce qu'on va chercher) : `AdviceViewModel.cs:347,405`, `AiOpinionRowViewModel.cs:114`
     → `AnswerCleaner.Extract(text, "BLOC :", "CONSEIL LIBRE")` ;
   - coloration (ce qu'on colore) : `AnswerLineViewModel.cs:10-12` (tableau statique
     `BLOC :, BAFFLE :, RÉGLAGES :, ALTERNATIVE :, CONSEIL LIBRE :, VERDICT :`).
   Traduire une couche sans les autres casse extraction **et** coloration. → §9 : source unique
   `AnswerFormat` + tolérance aux deux langues.

4. **Langue des invites IA.** `CataloguePrompt.cs:57` et `ArbitrationPrompt.cs:38` imposent
   « Réponds en français ». Un UI EN doit recevoir des réponses EN, **sinon pensée et verdict
   restent en français dans une interface anglaise**. → §9.

5. **Tests qui assertent du français (16).** Tant que le service localise avec FR **par défaut**
   et que l'extraction des chaînes est verbatim, ces tests restent verts **non modifiés** — c'est
   la preuve même de la non-régression. Mais ils dépendent implicitement de la culture de la
   machine : il faut **caler la culture de la suite de tests** (§10, G3).

6. **Compteurs et pluriels.** `$"{presets.Count:N0} preset(s)…"` : le hack « (s) » doit devenir de
   vrais pluriels (`n = 1 → « 1 preset »`, `n = 0 → « 0 preset »` en FR ; `1 preset`/`0 presets`/
   `2 presets` en EN), et `:N0` est sensible à la culture (« 2 310 » en FR, « 2,310 » en EN).

7. **Badges gratuit/payant.** Le **coloriage est déjà sûr** : il passe par
   `Classes.free`/`Classes.paid` liés aux booléens `IsFree`/`IsPaid`
   (`SettingsView.axaml:100-101,160`), pas par le texte. Le **libellé** et `Tag`
   passent par `Model.Tier.Free` / `Model.Tier.Paid` **depuis la phase 5**
   (audit §8.4 soldé : le texte n'est jamais une donnée de style).

Bonus : les messages `Échec : {exception.Message}` (`MainViewModel.cs:128`) contiennent des
messages **du runtime .NET**, qui suivent `CurrentUICulture` automatiquement (ICU) — en EN,
l'exception s'affichera déjà en anglais ; seul le préfixe « Échec » est notre clé.

---

## 4. Choix d'architecture (comparatif puis décision)

### 4.1 Options étudiées

| Option | Mécanisme | Pour | Contre |
|---|---|---|---|
| **A. ResX + assemblies satellites** (retenue) | `Strings.resx` (neutre = **en**) dans l'assembly principal, `Strings.fr.resx` → satellite `fr/` | Standard .NET ; **chaîne de repli gérée par la plateforme** (clé absente → EN, jamais vide) ; rien à charger à la main ; outillage IDE ; `ResourceManager` = API éprouvée ; pas de trimming dans notre publish | XML verbeux ; accès typé fort nécessite un générateur (ou `.GetString`) ; culture figée par satellite (fine pour fr/en) |
| B. Dictionnaires JSON/YAML | Fichiers chargés au démarrage, service maison | Simple, diff-friendly pour traducteurs | Repli, formatage, pluriels **tout à réinventer** ; hors standards .NET ; pas d'IDE |
| C. `ResourceDictionary` XAML par langue | Dictionnaires Avalonia fusionnés/échangés | Natif Avalonia, bascule triviale | Accès C# périlleux ; pas de `{0}` ni de pluriel ; divergences faciles FR/EN |
| D. `Microsoft.Extensions.Localization` (`IStringLocalizer`) | Couche officielle au-dessus de ResX | API standardisée, pensée DI | Pensé pour ASP.NET/Blazor ; notre app n'a **pas de conteneur DI** ; n'apporte rien qu'une couche fine ne fasse pas ; dépendance inutile |

### 4.2 Décision : **ResX (neutre = anglais) + un `Localizer` fin**

**Décision 3 (repli EN)** : toute langue ou clé manquante retombe sur **l'anglais** — y compris
le français lui-même si une clé FR venait à manquer. La chaîne de repli de `ResourceManager` est
*Culture → parent → neutre* ; pour que le dernier maillon soit l'EN, **le neutre est EN** :

- **`Strings.resx` = anglais**, embarqué dans `TonexAdvisor.App.dll` : toujours présent, zéro
  fichier externe — c'est le garde-fou contre tout trou.
- **`Strings.fr.resx`** → assembly satellite `fr\…resources.dll`, copié automatiquement par
  `dotnet publish` dans le portable et l'installeur (vérification en §12, phase 10).
- Le français reste **langue de référence UI** : parité de clés exigée avec l'EN (test G5) et
  extraction verbatim — une clé FR absente n'arrive que si la parité est rompue, et elle
  retomberait sur l'EN au lieu d'afficher un trou.
- **`Localizer`** : service mince (singleton) au-dessus de `ResourceManager` qui apporte ce que
  ResX ne donne pas seul : `PropertyChanged` à la culture (bascule à chaud), indexeur pour le
  XAML, formatage argumenté, pluriels.
- La **dépendance D reste possible** plus tard (le `Localizer` pourra exposer la forme
  `IStringLocalizer`) sans rien changer aux écrans.

Pourquoi c'est « state of the art » : repli géré par la plateforme (pas par nous), ressources
compilées, culture par `CurrentUICulture` (ICU/CLDR derrière), pluriels selon **CLDR**, zéro
dépendance externe, testable sans UI. Le neutre-EN est de plus la convention standard des
projets bilingues.

---

## 5. Contrat du `Localizer` (maquette d'API — pas du code livré)

```csharp
// Maquette indicative, à affiner à la phase 1.
sealed class Localizer : INotifyPropertyChanged
{
    static Localizer Instance { get; }           // point d'accès (pas de DI container dans l'app)
    CultureInfo Culture { get; set; }            // bascule = lève PropertyChanged("Item[]")
    string this[string key] { get; }             // lookup : culture → EN (neutre) → clé, jamais vide
    string Get(string key, params object?[] a);  // string.Format avec la culture courante
    string Plural(int n, string baseKey);        // rules fr/en (§6)
    event Action? CultureChanged;                // pour recalculer les chaînes composées
}
```

- **XAML** : extension `{loc:Loc Cle.De.Cle}` qui retourne un `Binding` sur l'indexeur — pas de
  `DataContext` requis, compatible avec les bindings compilés du projet (l'extension produit un
  `Binding` réflexion léger sur une clé statique, coût négligeable).
- **C#** : les ViewModels lisent `Localizer.Instance[...]` ou reçoivent la valeur déjà calculée ;
  les chaînes **composées** (états, titres) sont **rebâties à la réception de `CultureChanged`**.
- **Défaut** : `Culture = fr` tant que le démarrage ne l'a pas dit autrement → les tests (qui ne
  passent pas par `Program.Main`) continuent de voir le français.
- **Journalisation** : en DEBUG, une clé inconnue est une clé ratée → `Debug.Fail` + retour de la
  clé elle-même (visible immédiatement, jamais un écran vide).

**Convention de clés** : hiérarchique, ASCII, triée, `Écran.Elément[.Variante]`
(ex. `Main.Tab.Library`, `Library.Filter.All`, `Settings.Section.Language`,
`Msg.NoLibrary`, `Msg.LoadSummary`, `Prompt.Instruction.Language`, `Marker.Bloc`).
Une seule table de clés partagée par FR et EN ; les deux fichiers sont **triés dans le même ordre**
(le test de parité §10 le vérifie).

---

## 6. Règles linguistiques (pluriels, nombres, dates)

**Pluriels (CLDR)**

| Culture | Règle |
|---|---|
| `fr` | singulier pour **0 et 1** (« 0 preset », « 1 preset »), pluriel sinon |
| `en` | singulier pour **1 uniquement** (« 1 preset »), pluriel sinon (« 0 presets », « 2 presets ») |

- Clés par paire : `X.one` / `X.other` (suffixes CLDR `one`/`other`), sélectionnées par
  `Plural(n, baseKey)`. Ajouter une langue = ajouter sa règle de pluriel dans un `switch`
  culture, **pas** toucher aux écrans.
- Les hacks `preset(s)` / `tone model(s)` (`LibraryViewModel.cs:362`) sont éliminés au profit de
  vraies paires de clés.

**Nombres**

- Tout affichage numérique passe par `:N0` **avec la culture courante** : « 2 310 » (FR,
  espace insécable fine par ICU) / « 2,310 » (EN). Jamais de format en dur.
- Les 33 sites de formatage sont inventoriés en phase 1 (script) puis convertis un par un.

**Dates/heures** : `ToLocalTime()` + format culture (rare : horodatages de session). Inchangé en
logique, seulement passé à la culture courante.

**Tri et recherche** : le `Tokenizer.Normalize` (désaccentuation) est côté **données**
(`TokenizerTests.cs:10` le prouve) : **aucun lien avec la langue de l'UI**, non modifié. Le tri
des grilles suit `CurrentCulture` automatiquement.

---

## 7. Périmètre de traduction

**À traduire (~250-320 clés)**

1. XAML : titres, en-têtes de colonnes, libellés de boutons, infobulles, placeholders, textes
   des cartes de détail, sections des réglages.
2. ViewModels : messages d'état, messages d'erreur affichés, notes (`SettingsNote`
   « génération 2 »…), titres d'avis (`SUGGESTION — {m}` / `VERDICT — ARBITRÉ PAR {m}`,
   `SUGGESTIONS DES VOIX` / `AVIS DES VOIX` — `AdviceViewModel.cs:297,349,407`), badges
   « gratuit »/« payant », aide des réglages.
3. Invites IA (Core) : consigne de langue + consignes de format + gabarit de sortie.
4. Marqueurs de protocole : voir §9.
5. Fenêtre principale, onglets, filtres « Tous » (affichage seul, §8.3).

**Ne traduit pas**

- Données BDD (presets, catégories, genres, dossiers), chemins, `Library.db`, GUID.
- Identifiants de modèles (`gpt-5.4`, `gemini`…), valeurs `V1`/`V2`, `stomp`, `TONEX`.
- Journaux/débogage, exceptions brutes de .NET (la partie `exception.Message` suit ICU tout
  seul), noms de propriétés, clés de `state.json` (sauf cas §8.2).
- Les tests eux-mêmes (voir §10).

---

## 8. Décisions de conception structurantes

### 8.1 Langue des invites IA

Core ne dépend pas de l'UI : la langue **entre par paramètre**
(`CataloguePrompt(..., lang)` / `ArbitrationPrompt(..., lang)`, enum interne `fr|en` ou
`CultureInfo` — pas de dépendance au `Localizer` dans Core). L'App passe la culture courante.
- Tests Core existants (qui assertent le français) construisent explicitement `fr` → verts non
  modifiés ; de nouveaux tests couvrent `en`.

### 8.2 Largeurs de colonnes : identifiants stables (PIÈGE n°1)

Aujourd'hui la clé de `PresetColumnWidths`/`ToneModelColumnWidths` **est l'en-tête affiché**
(comparaison `Ordinal`). Migration :

1. Chaque colonne reçoit un **id stable** (`name`, `category`, `folder`, `settings`…) utilisé
   comme clé de persistance — indépendant de la langue.
2. Au chargement : clé = id → appliquée ; clé = **ancien en-tête FR ou EN** (compat) →
   traduite en id, appliquée, ré-écrite au prochain save (lecture-modification-écriture déjà en
   place, qui préserve le reste de l'état).
3. Test : un blob `state.json` v1.2.1 avec les en-têtes français charge et applique ; un second
   save convertit en ids ; **changer de langue ne perd pas les largeurs**.

### 8.3 Sentinelle « Tous » (PIÈGE n°2)

La constante `AnyFilter` **garde sa valeur** (identité logique, comparaisons, restauration
d'état : `state.Category = SelectedCategory == AnyFilter ? "" : …`). Seul l'**affichage** dans
la ComboBox se localise : l'item sentinelle est présenté par un gabarit/label
(`FilterChoice { Value, Label = L["Library.Filter.All"] }` ou template conditionnel).
Test de non-régression : aller-retour de `state.Category` inchangé quelle que soit la langue.

### 8.4 Badges gratuit/payant

Coloriage inchangé (classes `free`/`paid` sur booléens — déjà hors texte). Libellé passé par la
clé `Model.Tier.Free` / `Model.Tier.Paid`. **Auditer** `OpenCodeModels.Tag` (« gratuit ») : s'il
sert à un style ou à une sélection quelque part, le remplacer par une clé stable `free`/`paid`
et réserver le texte à l'affichage. (Inventaire en phase 1.)

---

## 9. Protocole des marqueurs IA — décision actée (décision 1)

**Option retenue : marqueurs localisés + tolérance aux deux langues.**

- Source unique **`AnswerFormat`** (dans Core, construite à partir de la culture de la session) :
  elle fournit **les trois couches** — liste des marqueurs à demander dans l'invite, marqueurs
  d'extraction, marqueurs à colorier. Plus jamais de littéral `"BLOC :"` éparpillé
  (`AdviceViewModel.cs:347,405`, `AiOpinionRowViewModel.cs:114`,
  `AnswerLineViewModel.cs:10-12` deviennent des appels à `AnswerFormat`).
- **Terminologie EN — validée (décision 1)** : `BLOC → BLOCK`, `BAFFLE → CAB` (cab sim),
  `RÉGLAGES → SETTINGS`, `ALTERNATIVE → ALTERNATIVE`, `CONSEIL LIBRE → FREE ADVICE`,
  `VERDICT → VERDICT`.
- **Tolérance** : `AnswerCleaner.Extract` reçoit la liste de candidats (culture active **+**
  repli) et cherche les deux. C'est aussi un robustesse **aujourd'hui** : un modèle qui répond
  par accident en anglais casse déjà l'extraction — la tolérance double la fiabilité.
- Numérotation des propositions (`1.`, `2.`) : unaffected par la langue (à l'exception du
  libellé de titre du verdict).

Tests dédiés : extraction FR, extraction EN, extraction d'une réponse FR dans une session EN et
vice-versa, coloration des deux jeux de marqueurs.

---

## 10. Persistance et compatibilité

- **`state.json`** : nouveau champ `UiLanguage` (**nullable string** `null | "fr" | "en"`).
  - `null` = jamais choisi → détection au premier lancement (§11) puis écriture.
  - Ancien fichier sans le champ → désérialisé sans erreur (`null`) : test avec un blob v1.2.1
    réel (les `PersistenceTests`/`UserStateTests` existants couvrent déjà le reste).
  - `Clone()` profond : inclure le nouveau champ (test).
- **`config.json`** : **aucun** changement (clés IA, modèles : hors sujet).
- **Largeurs de colonnes** : migration des clés §8.2.
- **Bases TONEX** : strictement hors sujet (lecture seule, empreintes).
- Ordre de grandeur de risque fichier : `state.json` est le **seul** fichier utilisateur touché,
  et en écriture additionnelle uniquement.

---

## 11. Détection, choix, bascule à chaud

**Premier lancement (`UiLanguage == null`)**

```
CurrentUICulture.TwoLetterISOLanguageName == "fr"  →  "fr"
tout le reste (en, en-US, de, es, pt…)             →  "en"   ← repli anglais (décision 3)
```

Puis la valeur est **persistée** ; ensuite c'est le choix explicite des réglages qui gagne.
Détection faite **avant** la première construction d'UI, dans `Program.Main`
(point d'entrée actuel, `Program.cs`) : fixer les quatre cultures
(`CurrentCulture`, `CurrentUICulture`, `DefaultThreadCurrentCulture`, `DefaultThreadCurrentUICulture`)
pour que les **threads arrière-plan** (lecture BDD, appels IA) formatent pareil.

**Choix dans les réglages** : un nouveau bloc court dans `SettingsView` (« Langue / Language »,
ComboBox FR/EN), posé en tête de la colonne des réglages, avec la mention que le changement est
immédiat.

**Bascule sans redémarrage — ce qui doit se recalculer** (liste fermée tirée de l'inventaire) :

1. Tous les `{loc:Loc}` XAML → auto (indexeur + `PropertyChanged("Item[]")`).
2. Chaînes composées dans les VM, abonnées à `CultureChanged` :
   `MainViewModel.StatusMessage` (`:85,101,115-120,128`), `LibraryViewModel.Summary`/`_emptyMessage`
   (`:52,362`), titres d'avis `AdviceViewModel` (`:297,349,407`), aides `SettingsViewModel`,
   `SettingsNote` du détail, libellés de modèles (`OpenCodeModels.Label`).
3. En-têtes de colonnes des DataGrid (via §8.2, les ids ne bougent pas).
4. Culture courante mise à jour → `:N0`, exceptions .NET, tri.
5. Les **marqueurs** et la **langue des invites** suivent : prochaine consultation IA dans la
   nouvelle langue (les réponses déjà affichées restent telles quelles — comportement assumé et
   documenté, pas d'appel IA à la volée).
6. Les **libellés de sentinelle des filtres** (gabarit `FilterLabelConverter`, §8.3, posé en
   phase 3) : le `Converter` ne se revalide pas de lui-même au changement de culture →
   reconstruire les listes de filtres à la bascule.

---

## 12. Plan de migration par étapes

**Chaque phase se termine par : `dotnet build` (warnings = erreurs) + 195(+n) tests verts.**
Aucun commit sans instruction. L'ordre est conçu pour que **la French UI ne change jamais** et
que les garde-fous arrivent **avant** la grande migration, pour l'encadrer.

| Phase | Contenu | Critère de sortie |
|---|---|---|
| **0 — Baseline** ✅ | Script d'inventaire exhaustif (liste CSV des clés : fichier, ligne, valeur FR, nature) ; figer l'état : 195 tests verts | **ATTEINT** : 566 lignes / 388 clés uniques, baseline 195/195 (§18) |
| **1 — Socle** ✅ | `Strings.resx` (neutre **EN**, clés créées dès maintenant) + `Strings.fr.resx` (extraction FR **verbatim**), `Localizer` (indexeur, `Get`, `Plural`, `CultureChanged`), extension `loc:Loc`, cadrage culture dans `Program.Main` + **calage de culture de la suite de tests** (fixture xunit) | **ATTEINT** : build 0/0 ; 204/204 tests verts dont **195 inchangés** (§18) |
| **2 — Garde-fous** ✅ | Test de **parité FR/EN** (clés, non-vides, `{0}` identiques) ; test **scan de littéraux** (XAML + C#) avec **allowlist par fichier** (fichiers pas encore migrés) | **ATTEINT** : deux tests verts (5 au total) ; chaîne en dur ajoutée → **attrapée** (sonde, §18) |
| **3 — Fenêtre principale + bibliothèque** ✅ | `MainWindow.axaml`, `LibraryView.axaml`, `LibraryViewModel` (y compris sentinelle §8.3, résumés `:N0`, pluriels) | **ATTEINT** : `MainWindow` (11) et `LibraryView` (53) sortis de l'allowlist, `LibraryViewModel` 5 → 1 (sentinelle seule) ; build 0/0 ; **209/209 tests verts ×12** (§18) |
| **4 — Vues de détail** ✅ | `PresetDetailView`, `ToneModelDetailView`, `PresetDetailViewModel` (dont `SettingsNote`) | **ATTEINT** : les 4 fichiers sortis de l'allowlist (13 + 12 + 15 + 2 → 0) ; build 0/0 ; **209/209 tests verts ×12** (§18) |
| **5 — Réglages (UI + VM)** ✅ | `SettingsView.axaml`, `SettingsViewModel` (le plus gros : 32 lignes accentuées), badges §8.4 | **ATTEINT** : les 4 fichiers sortis de l'allowlist (37 + 37 + 2 + 4 → 0) ; build 0/0 ; **209/209 tests verts ×12** (§18) |
| **6 — Avis IA (UI)** ✅ | `AdviceViewModel` (titres, spinner, états), `AiOpinionRowViewModel`, lignes de réponse | **ATTEINT** : les 2 fichiers de lignes sortis de l'allowlist (`AdviceCombinationRowViewModel` 4 → 0, `AdvicePresetRowViewModel` 3 → 0) et les 2 autres réduits à leurs seuls littéraux de protocole (`AdviceViewModel` 29 → 4, `AiOpinionRowViewModel` 4 → 2 — extraction `BLOC :`/`VERDICT :` reportée à la phase 7) ; build 0/0 ; **209/209 tests verts ×12** (§18) |
| **7 — Protocole IA (le plus délicat)** ✅ | `AnswerFormat` unique, invites Core paramétrées en langue (§8.1), marqueurs tolérants (§9), `AnswerLineViewModel` | **ATTEINT** : extraction/coloration FR **et** EN verts (**19 nouveaux tests** : `AnswerFormatTests` 17 dont 8 de coloration, `EnglishPromptTests` 2) ; les tests français de protocole **non modifiés et verts** ; build 0/0 ; **228/228 tests verts ×12** (§18) |
| **8 — Passe nombres/dates/pluriels** ✅ | Les 33 sites de formatage, élimination de `(s)` | **ATTEINT** : plus aucun format en dur (dates `dd/MM/yyyy` → `g` de culture, `Mo`/`MB` par règle de culture §11), les 7 sites culture-sensibles de l'inventaire (§2) traités ; les 7 `(s)` des resx éliminés (6 clés en paires `.one/.other`, 1 en colle `{0} · {1}` + compteurs) ; 9 clés neuves ; build 0/0 ; **232/232 tests verts ×12** (§18) |
| **9 — Langue : détection, choix, bascule** ✅ | `UiLanguage` + rétrocompat (§10), ComboBox réglages, recalcul à chaud (§11), largeurs de colonnes en ids (§8.2) | **ATTEINT** : persistance `UiLanguage` + blob v1.2.1 (`null` = jamais choisi) + `Clone()` ; détection « fr »→fr / tout le reste → en écrite **avant l'UI** ; choix en tête des réglages, bascule immédiate (4 cultures) ; liste fermée §11.4 câblée et **verte** (statut, résumé/vide, filtres §11.6, détail, titres d'avis, aides réglages, libellés de modèles) ; largeurs FR/EN → ids §8.2 (vue en ids, allowlist **inchangée**) ; 4 clés neuves ; build 0/0 ; **251/251 tests verts ×12** (§18) |
| **10 — EN complet + recette** ✅ | `Strings.resx` (neutre EN) et `Strings.fr.resx` remplis à 100 % (parité = 100 %), allowlist = **vide**, vérification satellite `fr\` dans le zip et l'installeur, recette manuelle des deux langues (§14) | **ATTEINT** : parité 100 % sur les **deux paires** — App **240 clés** et **paire Core créée 180 clés** (invites, marqueurs, raisons, potards, origine — arbitrage « Ressources Core », §17 n°5) ; allowlist **VIDE : 0 fichier / 0 littéral** (de 26/194) avec le scan G6 **affiné** (8 marqueurs techniques retirés, mesure à l'appui ; exemption étendue aux deux moteurs de clé — arbitrage §17 n°6) ; les deux satellites `fr\` (App + Core) présents dans le **zip** et la charge utile de l'**installeur** (`artifacts\…1.2.1-…` régénérés) ; fumée exe FR **et** EN vertes, `state.json` réel préservé ; recette §14 **prête pour signature** (par vos soins) ; build 0/0 ; **254/254 tests verts ×12** (§18) |
| **11 — Installeur FR/EN** | `tools/Setup` : ~12 chaînes en **table FR/EN en code**, choisie par culture OS avec la même règle que §11 (`fr`→FR, sinon EN) — un tableau en code et non des ressources parce que `PublishSingleFile=true` exclut les assemblies satellites ; description des raccourcis localisée à l'installation | Test de couverture (aucune chaîne sans EN ni FR), sortie console dans la langue de l'OS, `build-release.ps1` inchangé |

**Livré hors phases** (décision 4, fait avec ce plan) : `README.md` + `README.en.md` croisés par
un lien de langue, faits et chiffres remis à jour des deux côtés (195 tests, délai de voix 180 s,
colonnes redimensionnables).

Estimation de volume par phase : 3 à 6 — migration « mécanique » fichier par fichier ;
7 et 9 concentrent le risque et sont isolées volontairement.

---

## 13. Garanties de non-régression (contrats vérifiables)

| # | Garantie | Mécanisme | Preuve |
|---|---|---|---|
| **G1** | Build toujours vert, warnings = erreurs | gate par phase | `dotnet build` à chaque fin de phase |
| **G2** | Les **195 tests existants** restent verts **sans modification** | FR par défaut dans le service + extraction verbatim | rapport `dotnet test` inchangé, fichiers de tests non touchés (sauf fixture culture, ajout) |
| **G3** | Tests indépendants de la machine | fixture culture = `fr-FR` pinée pour toute la suite (AUJOURD'HUI la suite suppose implicitement une machine FR) | nouveau `CultureFixture` |
| **G4** | FR ≡ UI actuelle caractère pour caractère | extraction verbatim + **test golden** sur un échantillon de clés critiques (messages d'état, en-têtes, marqueurs) | `LocalizationTests` |
| **G5** | FR **et** EN complets et cohérents (EN = ressource neutre, maillon de repli final) | parité de clés, non-vidité, parité des placeholders `{0}` | test de parité (phase 2), **étendu en phase 10 aux deux paires : App 240 clés + Core 180 clés** |
| **G6** | Aucune nouvelle chaîne en dur | scan de littéraux XAML/C# avec allowlist par fichier → **vide** en phase 10 | test scan — allowlist **vide depuis la phase 10** (0 fichier / 0 littéral), regex affinée sur mesure + exemption des deux moteurs de clé (`Localizer.`, `CoreTexts.`) |
| **G7** | Protocole IA intact dans les deux langues | `AnswerFormat` unique + tests d'extraction/coloration croisés FR/EN | `LocalizationTests` / `AnswerCleanerTests` étendus |
| **G8** | État utilisateur préservé | tests : ancien `state.json` sans `UiLanguage` ; clés de colonnes FR historiques converties ; aller-retour filtres inchangé ; `Clone()` complet | `PersistenceTests`/`UserStateTests` étendus |
| **G9** | Bascule à chaud complète | test unitaire : changer `Culture` → `PropertyChanged("Item[])"` + `CultureChanged` + recalcul des chaînes composées listées en §11 | `LocalizationTests` + `BasculeTests` (phase 9) |
| **G10** | Installeur bilingue, aucune chaîne orpheline | table FR/EN couverte à 100 % (test de couverture), satellite `fr\` présent dans zip **et** installeur | test Setup + checklist release |

**Volumes** : 195 tests actuels + **~20 à 30** nouveaux (socle, parité, scan, persistance,
marqueurs, bascule). Le compte exact est annoncé honnêtement à chaque phase, comme jusqu'ici.

**Ce qui n'est PAS automatisable** : le rendu visuel final. Aucune simulation de souris ni
automatisation d'UI (règle de travail) — la recette est **manuelle par vos soins** (§14).

---

## 14. Recette manuelle (checklist — par vos soins, sans automatisation)

Pour chaque langue (FR, EN), après phase 10 :

- [ ] Premier lancement sur machine EN → UI EN ; machine FR → UI FR ; relance → la langue choisie
      tient.
- [ ] Bascule FR↔EN dans les réglages **sans redémarrage** : onglets, grilles, en-têtes, cartes de
      détail, réglages, messages d'état, conseils IA déjà affichés (inchangés, normal).
- [ ] Aucun débordement de texte EN (boutons, en-têtes de colonnes, chip « gratuit ») — l'EN est
      souvent **plus long** ; carte de détail fixée à 400 px.
- [ ] Filtres catégorie/genre/dossier : « Tous »/« All » affiché correctement, filtres
      fonctionnels, `state.json` inchangé après aller-retour.
- [ ] Largeurs de colonnes : conservées après bascule et après redémarrage (ids stables).
- [ ] Avis IA en EN : réponse EN, marqueurs `BLOCK :`/`CAB :` colorés, verdict propre ; repasser
      en FR : marqueurs français colorés.
- [ ] Compteurs : « 2 310 » (FR) / « 2,310 » (EN), pluriels corrects (0/1/2).
- [ ] Portable **et** installeur : dossier `fr\` présent (satellite français) ; sans lui, l'UI
      bascule en EN au lieu d'échouer (repli).
- [ ] Installeur : machine FR → console en français ; machine EN ou autre → anglais (même règle
      que l'application), description des raccourcis dans la langue du jour.
- [ ] README : lien de langue visible dans les deux sens (`README.md` ↔ `README.en.md`).
- [ ] 195+20 tests verts, build sans warning.

---

## 15. Risques et parades

| # | Risque | Gravité | Parade |
|---|---|---|---|
| R1 | Largeurs de colonnes perdues au passage EN | Haute | ids stables + compat anciennes clés (§8.2), test G8 |
| R2 | Cassure du protocole IA (extraction/coloration) | Haute | `AnswerFormat` unique + tolérance deux langues (§9), tests G7 |
| R3 | Dérive FR (« non-régression » rompue) | Haute | extraction verbatim + golden G4 + 16 tests intouchés G2 |
| R4 | Tests cassés selon la machine de CI/dev | Moyenne | fixture culture G3 |
| R5 | Sentinelle « Tous » traduite → filtres cassés | Moyenne | séparation identité/étiquette (§8.3), test G8 |
| R6 | Chaînes composées oubliées à la bascule (texte resté FR) | Moyenne | liste fermée §11 + allowlist de scan qui ne laisse rien passer (G6) + recette manuelle |
| R7 | EN plus long → débordements | Moyenne | recette §14, points de vigilance : colonnes DataGrid, carte 400 px, chips |
| R8 | Satellite `fr\` absent de l'artefact release → un francophone verrait l'UI en EN | Moyenne | checklist phase 10 dans `build-release.ps1`/zip |
| R11 | Installeur single-file : les assemblies satellites sont impossibles → chaîne EN oubliée | Moyenne | table FR/EN en code + test de couverture (phase 11, G10) |
| R9 | Pluriels/nombres faux dans une culture | Basse | règles CLDR §6 + tests dédiés |
| R10 | Tentation de « corriger » le français en passant par les ressources | Basse | règle explicite : le FR ressource = copie verbatim, toute reformulation est un autre ticket |

---

## 16. Critères d'acceptation (au terme de la phase 11)

1. FR affiché **identique** à v1.2.1 (golden + recette) ; EN **complet** (parité 100 %) ;
   clé ou langue inconnue → **repli EN** (décisions 2 et 3).
2. Détection au premier lancement (`fr`→FR, **tout le reste → EN**) + choix persisté + bascule à
   chaud (§11).
3. Contrats **G1 → G9** tous vérifiés ; suite complète verte ; build sans warning.
4. `state.json` d'un utilisateur v1.2.1 chargé sans perte (filtres **et** largeurs de colonnes).
5. Avis IA dans la langue de l'UI, marqueurs colorés dans les deux sens.
6. Aucune chaîne utilisateur en dur (allowlist de scan vide).
7. Artefacts portable + installeur contenant le satellite `fr\` ; installeur **en français et en
   anglais** (console + description des raccourcis, règle de détection identique à l'app).
8. `README.md` et `README.en.md` croisés par un lien de langue, mêmes faits, mêmes chiffres.

---

## 17. Décisions actées (04/10/2026)

Les quatre points ouverts ont été tranchés par l'utilisateur ; leurs conséquences sont déjà
intégrées dans le plan (les sections ci-dessus sont à jour).

| # | Point | Décision | Impact dans le plan |
|---|---|---|---|
| 1 | Terminologie des marqueurs EN | **Validée** : `BLOCK / CAB / SETTINGS / FREE ADVICE / VERDICT` | §9 — marqueurs localisés + tolérance aux deux langues |
| 2 | Langue de l'installeur | **Français et anglais** | §12 phase 11 (table FR/EN en code, single-file), G10, §14, §16 |
| 3 | Repli pour une 3ᵉ langue | **Anglais** (et non français) | §4.2 — ressource **neutre = EN** ; §11 — détection `fr`→FR, tout le reste → EN |
| 4 | README anglais | **Immédiatement** | `README.en.md` livré avec ce plan + lien croisé dans les deux fichiers |
| 5 | Où vivent les textes construits en Core (invites IA, raisons, libellés de potards, marqueurs) | **Ressources Core** : paire `Strings.resx`/`Strings.fr.resx` dans Core, lue sans le `Localizer` de l'App — culture en paramètre pour les invites (§8.1 intact), culture ambiante cadrée pour le reste | §12 phase 10, G5 (parité des deux paires), satellite `fr\TonexAdvisor.Core.resources.dll` ajouté au zip |
| 6 | Faux positifs techniques du scan G6 (SQL, identifiants, chemins, user-agent, sentinelle « Tous », « TextBlock », vocabulaire) | **Affinage de la regex** : 8 marqueurs retirés après mesure (zéro ligne d'affichage de l'inventaire perdue) + exemption étendue aux lignes `CoreTexts.` comme `Localizer.` | §13 G6 (allowlist vidée), `ScanLitterauxTests` + miroir `tools\inventaire-i18n.ps1` |

Aucune question bloquante restante : les 6 décisions ci-dessus sont intégrées dans le plan.

---

## 18. Suivi d'exécution

| Date | Étape | État | Preuve / artefacts |
|---|---|---|---|
| 04/10/2026 | Plan + décisions (§17) | ✅ fait | ce document, `README.en.md` |
| 04/10/2026 | **Phase 0 — Baseline + inventaire** | ✅ fait | baseline : `dotnet build` 0 erreur, **195/195 tests verts** ; inventaire : `inventaire-i18n.csv` — **566 lignes**, dont **475 à migrer** (1 sentinelle §8.3) et **91 hors-champ**, **388 clés uniques** ; générateur `tools\inventaire-i18n.ps1` ; brut `inventaire-i18n-raw.csv` (397) conservé pour contrôle de dérive |
| 04/10/2026 | **Phase 1 — Socle (`Strings.resx` + `Localizer`)** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **204/204 tests verts** (195 inchangés + 9 nouveaux socle), **12/12 exécutions consécutives vertes sur le code final** (36/36 pendant la stabilisation, cf. notes dispatcher ci-dessous) ; artefacts : `src\TonexAdvisor.App\Localization\{Strings.resx, Strings.fr.resx, Localizer.cs, LocExtension.cs}`, cadrage culture `Program.FrameCulture` (fr statu quo), `<NeutralResourcesLanguage>en</NeutralResourcesLanguage>`, tests `TestCulture` (ModuleInitializer fr-FR), `CultureGuard`, `LocalisationCollection`, `LocalizerTests` (7), `LocExtensionTests` (2) |
| 04/10/2026 | **Phase 3 — Fenêtre principale + bibliothèque** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **209/209 tests verts** (aucun ajout : 204 socle/garde-fous + 5 de phase 2), **12/12 exécutions consécutives vertes** ; sortie d'allowlist : `MainWindow.axaml` **11 → 0**, `LibraryView.axaml` **53 → 0**, `LibraryViewModel.cs` **5 → 1** (sentinelle §8.3 seule) ; **59 clés FR/EN** ajoutées en paires ordonnées (**71 au total**, parité vérifiée) ; sentinelle : valeur `AnyFilter` intacte + affichage localisé (`FilterLabelConverter`, 3 ComboBox, §8.3) ; artefacts : `xmlns:loc` + `{loc:Loc}` (2 XAML), `Localizer.Instance.Get/[]` (VM), `src\TonexAdvisor.App\Localization\FilterLabelConverter.cs`, allowlist régénérée **38 fichiers / 332 littéraux** |
| 05/10/2026 | **Phase 4 — Vues de détail** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **209/209 tests verts** (aucun ajout), **12/12 exécutions consécutives vertes** ; sortie d'allowlist : `PresetDetailView.axaml` **13 → 0**, `ToneModelDetailView.axaml` **12 → 0**, `PresetDetailViewModel.cs` **15 → 0**, `ToneModelDetailViewModel.cs` **2 → 0** ; **27 clés `Detail.*`** ajoutées en paires ordonnées (**98 au total**, parité vérifiée) ; allowlist **34 fichiers / 290 littéraux** ; artefacts : `xmlns:loc` + `{loc:Loc}` (2 vues), `Localizer.Instance.Get/[]` (2 VM), constantes techniques `Sep`/`SepLarge`/`PrefixHwA`/`PrefixHwB` |
| 05/10/2026 | **Phase 5 — Réglages (UI + VM)** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **209/209 tests verts** (aucun ajout), **12/12 exécutions consécutives vertes** ; sortie d'allowlist : `SettingsView.axaml` **37 → 0**, `SettingsViewModel.cs` **37 → 0**, `SettingsView.axaml.cs` **2 → 0**, `OpenCodeModels.cs` **4 → 0** (badges §8.4) ; **76 clés** ajoutées en paires ordonnées (**174 au total**, parité vérifiée) ; allowlist **30 fichiers / 210 littéraux** ; artefacts : 43 sites `{loc:Loc}` (dont 6 boutons à texte élément), `Localizer.Instance.Get/[]` (VM + code-behind), `OpenCodeModel.TierLabel` → `Model.Tier.Free`/`.Paid`, clé d'invite `Invite.Ping` |
| 05/10/2026 | **Phase 6 — Avis IA (UI)** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **209/209 tests verts** (aucun ajout), **12/12 exécutions consécutives vertes sur le code final** (course de culture corrigée en cours de route, cf. notes) ; sortie d'allowlist : `AdviceCombinationRowViewModel.cs` **4 → 0**, `AdvicePresetRowViewModel.cs` **3 → 0** ; réduits aux seuls littéraux de protocole : `AdviceViewModel.cs` **29 → 4**, `AiOpinionRowViewModel.cs` **4 → 2** (extraction `BLOC :`/`VERDICT :` laissée à la phase 7) ; **29 clés `Conseil.*`** ajoutées en paires ordonnées (**203 au total**, parité vérifiée) ; allowlist **28 fichiers / 176 littéraux** ; artefacts : `Localizer.Instance.Get/[]` (4 VM), `Plural` branché sur `Conseil.Combination.Uses.one/.other` (graines phase 1), constante `GenreSentinelle`, `tests\TonexAdvisor.Core.Tests\AssemblyInfo.cs` (parallélisme xunit désactivé) |
| 05/10/2026 | **Phase 7 — Protocole IA** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **228/228 tests verts** (209 + 19 nouveaux), **12/12 exécutions consécutives vertes sur le code final** ; sortie d'allowlist : `AnswerLineViewModel.cs` **6 → 0**, `AdviceViewModel.cs` **4 → 0**, `AiOpinionRowViewModel.cs` **2 → 0** ; entrée neuve `AnswerFormat.cs` **12** (table des 6 marqueurs FR/EN : les seuls littéraux du fichier) ; invites Core bilingues en code : `CataloguePrompt.cs` **23 → 34** et `ArbitrationPrompt.cs` **18 → 29** (les branches EN vivent sur des lignes françaises — comptées, miroir mécanique du scan) ; allowlist **26 fichiers / 198 littéraux** ; tests français de protocole **inchangés** ; artefacts : `src\TonexAdvisor.Core\Advice\AnswerFormat.cs` (3 couches §9 : invite = langue de la session, extraction + coloration = les deux), surcharge tolérante `AnswerCleaner.Extract(text, startMarkers, endMarkers)` (session d'abord + repli), invites `(..., CultureInfo? culture = null)` §8.1 (défaut = fr, tests Core inchangés), culture passée par l'App (`Localizer.Instance.Culture`), tests `AnswerFormatTests` + `EnglishPromptTests` |
| 05/10/2026 | **Phase 8 — Nombres/dates/pluriels** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **232/232 tests verts** (228 + 4 nouveaux `PluralisationTests`), **12/12 exécutions consécutives vertes sur le code final** ; dates : `Reglages.Fichier.Info` `{1:dd/MM/yyyy HH:mm}` → `{1:g}` (date courte + heure de la culture ; rendu fr identique) et `LibraryFile.ModifiedLabel` → `ToString("g", CultureInfo.CurrentCulture)` ; unité `Mo`/`MB` par règle de culture §11 (`LibraryFile.SizeUnit`, pas de `Localizer` dans Core) ; pluriels : les **7 `(s)` des resx éliminés** — 6 clés en paires `.one/.other` (`Conseil.Avis.SansArbitre`, `Conseil.Indice.Combinaison`, `Conseil.Indice.PresetSeul`, `Detail.BlocsActifs`, `Reglages.Avis.VoixDisparues`, `Reglages.Bases.Trouvees`), `Message.Resume.Bibliotheque` et `Reglages.Compteurs` réduites à la colle `{0} · {1}` ; **9 clés neuves en paires ordonnées** (`Compteur.Presets`/`Compteur.ToneModels` `.one/.other`, `Message.Resume.Chargement`/`.Jointe`/`.NonDispo`, `Conseil.Score.Pourcent`, `Voix.Duree`) → **218 clés au total**, parité vérifiée ; `Plural(n, baseKey, params args)` étendu pour les clés à `{1}` ; sites migrés : ligne de statut `MainViewModel` (allowlist **11 → 7**, total **26 fichiers / 194 littéraux**), `CountsLabel`, `TonexFolderHint`, `VoiceModelsStatus`, `Summary`, `ActiveBlockLine` (passé en `int`), `ScoreLabel` ×2, `ElapsedLabel` ; tests mis à jour **consciemment** : `LocalizerTests.Formatage_…` recomposé via `Plural` (son objet — le `:N0` de culture — intact), `VoiceAndModelTests` à l'accord singulier ; reste en `(s)` : `LibraryAdvisor:133` (raisons Core, phase 10) |
| 04/10/2026 | **Phase 2 — Garde-fous (parité + scan)** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **209/209 tests verts** (204 + 5 nouveaux), **12/12 exécutions consécutives vertes** ; garde-fou G6 **prouvé par la sonde** : ajout d'une chaîne en dur (`SondeScanTemporaire.cs`) → test rouge « hors allowlist », sonde retirée → vert ; artefacts : `PariteResxTests` (3 tests : clés, non-vidité, emplacements `{0}`), `ScanLitterauxTests` (2 tests : compteur exact vs allowlist + entrées périmées), `tests\TonexAdvisor.Core.Tests\litteraux-allowlist.txt` (**34 fichiers / 344 littéraux**), `TestPaths.RepoRoot` (racine via `TonexAdvisor.sln`) |
| 05/10/2026 | **Phase 9 — Langue : détection, choix, bascule** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **251/251 tests verts** (232 + 19 nouveaux : `UiLanguagesTests` 6, `BasculeTests` 8, `ColumnIdsTests` 4, blob v1.2.1 `UserStateTests` 1), **12/12 exécutions consécutives vertes sur le code final** ; détection §11 : `UiLanguages.Resolve` — choix mémorisé d'abord, sinon « fr »→fr et **tout le reste→en** — persistée par `Program.FrameCulture` **avant toute construction d'UI** (écriture `state.json` tolérante IO) ; choix : bloc « LANGUE » en tête des réglages (`Reglages.Langue.*`, **4 clés en paires ordonnées → 222 clés**, parité vérifiée), ComboBox FR/EN transportant le **code** (`LanguageLabelConverter`, identité ≠ libellé §8.3) qui bascule `Localizer.Instance.Culture` (4 cultures) **et** persiste `state.UiLanguage` sur le champ ; liste fermée §11.4 câblée : compositeurs `StatusMessage` (4 sites) + `VoicesTitle`/`AiTitle`/`AiStatus` (9 sites), `ComposeSummary` extrait de `Refresh`, listes de filtres reconstruites à la bascule (§11.6, sélection préservée), détail **reconstruit** (chaînes datent de sa création, `ShowHardware` repris), aides réglages re-notifiées + résumé extrait en `RefreshSummaryLabels`, libellés de voix et cellules de modèles repeints ; largeurs §8.2 : `ColumnIds` (id ↔ en-tête FR/EN via `GetForCulture`), normalisation **à la lecture et à l'écriture**, la vue ne voit que des ids — allowlist **inchangée : 26 fichiers / 194 littéraux** ; tests `PersistenceTests` mis à jour **consciemment** (3 : en-têtes FR → ids) ; fumée exe : fenêtre créée, arrêt propre, `state.json` réel préservé (sauvegarde/restauration) |
| 05/10/2026 | **Phase 10 — EN complet + recette** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **254/254 tests verts** (251 + 3 de parité en théories) ; **allowlist VIDE : 0 fichier / 0 littéral** (de 26/194) — le garde-fou G6 ne laisse plus passer une seule chaîne en dur ; **paire Core créée** (`src\TonexAdvisor.Core\Localization\Strings{,.fr}.resx`, **180 clés**, `CoreTexts` sans dépendance au `Localizer` §8.1 : culture en paramètre pour les invites, ambiante cadrée pour raisons/potards/origine, `Plural` CLDR en miroir) ; **App portée à 240 clés** (`Erreur.IA`, `Erreur.CleAbsente`, `Invite.Ping` réutilisée, `Message.ChoisirFichier`/`Lecture`/`DatabaseLabel.*`, `Voix.Erreur.*`, `Libelle.Type.*` ×6, `Libelle.Canal.Suffixe`) ; migrés : invites des deux prompts (ternaires FR/EN → clés, verbatim des deux branches), 6 marqueurs §9, 25 raisons du Conseil (`Raison.*` — dont le `(s)` de `LibraryAdvisor:133` soldé en `Raison.Uses.one/.other`), **106 libellés/sections de potards** (`Potard.*` — `Label`/`Title` calculés à la lecture, jamais figés au démarrage), origine de jointure, statuts `MainViewModel` (7), messages d'erreur IA, « canal », types de tone model ; **scan G6 affiné** : 8 marqueurs techniques retirés (`presets?`, `Favori`, `Tous`, `BLOC`, `ALTERNATIVE`, `Tonex`, `tone model`, `mod[eè]le`) — mesure : **zéro ligne d'affichage de l'inventaire perdue** — + exemption étendue à `CoreTexts.` (miroir `tools\inventaire-i18n.ps1` mis à jour) ; parité **étendue aux deux paires** (+3 tests) ; satellites `fr\` **présents dans le zip et la charge utile de l'installateur** (`artifacts\TonexSongAdvisor-1.2.1-{portable.zip,setup.exe}` régénérés) ; fumée exe FR **et** EN vertes (`UiLanguage=en` temporaire, `state.json` restauré) ; **recette §14 prête pour signature** (par vos soins) |

**Notes techniques de la phase 1** (constats mesurés, à connaître pour les phases 3 à 9) :
- **Graine de 12 clés** extraite verbatim dans les deux resx (ordre identique) : `Biblio.Col.Nom`,
  `Biblio.Detail.Vide.Titre`, `Biblio.Recherche.Titre`, `Fenetre.Nav.Bibliotheque`, `Filtre.Tous`,
  `Marqueur.Bloc` (EN `BLOCK :`), `Marqueur.Verdict`, `Message.AucuneBibliotheque`,
  `Message.Echec`, `Message.Resume.Bibliotheque` (+ emplacements `{0:N0}`), et la paire de
  pluriels `Conseil.Combination.Uses.one/.other` (créée d'emblée : le `Plural` du socle doit
  être testable ; le clé unique d'inventaire devient la clé de base). Les autres clés arrivent
  au fil des phases 3-8, en paires FR/EN (parité assurée à chaque étape).
- **Notification d'indexer** : le plan §5 retenait `Item[]` (convention WPF) — grille de
  diagnostic faite : **Avalonia 12 n'écoute que `Item`** sur une source INPC. Le `Localizer`
  lève les deux (`Item[]` + `Item`), couverture garantie quel que soit le consommateur.
- **Abonnement INPC = le point le plus sensible de la phase** : `CollectionNodeBase`
  s'abonne via `WeakEvents.ThreadSafePropertyChanged`, qui livre `PropertyChanged`
  **synchrone si `Dispatcher.UIThread.CheckAccess()`**, sinon `Post` — jamais exécuté en
  suite headless, personne ne pompe. Le premier test à toucher le dispatcher en possède la
  propriété (`s_uiThread ??= this`) : sans précaution, la bascule `fr→en` ne se propageait
  plus à l'expression (échec aléatoire en suite complète, vert en isolation — mesuré).
  Remède (`LocExtensionTests.ReclamerLeDispatcher`) : `Dispatcher.ResetBeforeUnitTests()`
  (API interne Avalonia = `ResetGlobalState`, sans fermeture de boucle — **`ResetForUnitTests`
  ferme la boucle partagée et fait échouer d'autres tests**, constaté en 12 exécutions) +
  réclamation du dispatcher par le fil du test, avec retries contre la course.
- **Livraison vers la cible** : `UntypedBindingExpressionBase.PublishValue` bascule sur le
  dispatcher (`CheckAccess` → synchrone, sinon `Post` ; la valeur de l'expression, elle, est
  posée **avant** le gate). Les tests du socle assertent donc la **valeur de l'expression** ;
  la poussée finale vers les contrôles se valide visuellement en phase 3. Dans l'application
  réelle, le fil UI possède le dispatcher dès l'amorçage — pas d'impact.
- **Lecture de la valeur du binding en test** : l'assembly de *référence* d'Avalonia 12.1.3
  n'expose pas `UntypedBindingExpressionBase.GetValue()` (absent au compile-time — CS1061/CS7036
  — alors qu'il est public en runtime sur `lib\net8.0`) → appel par réflexion
  (`LocExtensionTests.ValeurDe`), avec lecture de la cible pour démarer l'expression.
- **Test du pluriel** : `Plural(n, baseKey)` met `{0}` au plat (l'appelant reçoit la phrase
  prête, culture `fr` : 0 et 1 au singulier ; `en` : 1 seul).
- **§6 (sites de formatage)** : 7 sites culture-sensibles tracés dans l'inventaire
  (`culture N0` ×4, dates ×2, `Mo/MB` ×1) ; le reste des 33 appels mesurés (§2) n'a pas
  d'impact de culture — conversion site par site en phase 8. ✅ Vendu en phase 8 (7/7 traités,
  plus aucun format en dur hors données invariantes §7).
- **§8.4 (audit `OpenCodeModels.Tag`)** : `Tag` = « gratuit »/« payant », affiché tel quel dans
  `SettingsView.axaml:99,162` (`{Binding Tag}`) ; `Label` = `{Id} · gratuit|payant` composite.
  Déjà inventoriés (`audit Tag`, `composite (Id + libellé)`) → clés traitées en phase 5.

**Notes techniques de la phase 2** (à connaître pour les phases 3 à 10) :
- **Règle de scan en miroir** : `ScanLitterauxTests` reproduit exactement les regex et seuils
  de `tools\inventaire-i18n.ps1` (phase 0) — couverture identique à l'inventaire, sans
  divergence possible entre le script et le test. **Assainie en phase 3 des deux côtés**
  (test + script, miroir préservé) : `RegexOptions.IgnoreCase` et exclusion des lignes
  `Localizer.` — détail dans les notes de la phase 3 ci-dessous.
- **Allowlist exacte, deux sens** : `litteraux-allowlist.txt` = `chemin;nombre` par fichier ;
  le test exige l'égalité dans les deux sens — tout dérive (ajout **ou** retrait) impose une
  mise à jour consciente. En phase 3+, un fichier migré descend à 0 → le test signale
  l'entrée périmée → sortie de l'allowlist (critère de sortie des phases).
- **Encodage** : le test lit en UTF-8 ; le script PowerShell 5.1 lit l'ANSI sur fichier sans
  BOM — comptages possiblement différents du brut `inventaire-i18n-raw.csv`. Sans impact :
  l'allowlist est générée par le scanner du test lui-même (auto-consistance garantie).
- **Couverture connue (limite assumée)** : une chaîne C# française **sans accent ni mot-clé
  du dictionnaire `frRe`** n'est pas détectée — même limite que l'inventaire de phase 0.
  Les attributs XAML (`Text`, `Header`, `Content`, `ToolTip.Tip`, `Watermark`,
  `PlaceholderText`) attrapent en revanche tout littéral non-`{`, accent ou non.
- **Parité sur les fichiers sources** : `PariteResxTests` lit les deux `.resx` directement
  (et non via `ResourceManager`) — le contrat porte sur les fichiers que les phases 3-10
  éditent. Garde anti-vide : `Assert.NotEmpty` sur les deux jeux de clés.

**Notes techniques de la phase 3** (rectifications et pièges, pour les phases 4 à 10) :
- **Deux assainissements de la règle de scan**, appliqués au test **et** au script
  (`tools\inventaire-i18n.ps1`) pour préserver le miroir, avec régénération de l'allowlist :
  1. `RegexOptions.IgnoreCase` sur `LigneFrancaise` : `-match` PowerShell est insensible à la
     casse, `Regex.IsMatch` ne l'est pas par défaut — sans quoi la règle de phase 2 n'était
     pas le miroir exact annoncé (constaté en phase 3) ;
  2. **exclusion des lignes contenant `Localizer.`** : après migration, les *clés* elles-mêmes
     se retrouvent sur des lignes francophones (`"Biblio.Vide.AucunPreset"` contient `Aucun`,
     `"…Filtre.Tous"` contient `Tous`) ; compter une clé comme « chaîne d'affichage »
     empêcherait **tout** fichier migré de sortir de l'allowlist. Une clé n'est pas un
     littéral d'affichage : la ligne qui la référence est hors scan.
- **Bilan de la régénération** : 34 fichiers / 344 littéraux → **38 / 332** — les 2 fichiers
  migrés sortis (−64) et la sentinelle VM réduite à 1 (−4), **contre +56** littéraux révélés
  par la correction de casse (6 fichiers nouveaux, lignes qui ne matchaient qu'en insensible
  à la casse). Le nouveau total est le vrai miroir de l'inventaire.
- **Sentinelle §8.3** : `AnyFilter` garde sa valeur `"Tous"` — les comparaisons
  (`category != AnyFilter`, `folder != AnyFilter`, restauration de `state.Category`) sont
  inchangées. L'affichage passe par `FilterLabelConverter` (un `ItemTemplate` sur chacune des
  3 ComboBox : `Text="{Binding Converter={StaticResource FilterLabel}}"`) : l'item sentinelle
  affiche la clé `Filtre.Tous`, tout le reste est de la **donnée utilisateur** (catégories,
  genres, dossiers réels de la bibliothèque) → jamais localisé.
- **Résumés `:N0`** : `Message.Resume.Bibliotheque` tel quel (`Instance.Get` formate avec la
  culture du `Localizer`), le suffixe `  —  filtre actif` a sa propre clé
  (`Biblio.Resume.FiltreActif`, espaces et cadratin préservés). **`(s)` inchangé** : leur
  élimination par pluriels CLDR relève de la **phase 8** (§5) — pas de reformulation ici.
  ✅ Vendu en phase 8 : `Message.Resume.Bibliotheque` est devenue la colle `{0} · {1}` servie
  par les compteurs `Compteur.Presets`/`Compteur.ToneModels` (vraies formes `one`/`other`).
- **Chaînes composées du VM** : `Summary`/`EmptyMessage` sont posés à la construction et au
  filtrage ; le **recalcul à chaud** reste la charge de la phase 9 (§11, entrées 2 et 6).
- **Boutons à texte élément** : `<Button>Texte</Button>` n'est pas compté par la regex
  d'attributs (d'où 53 et non 59 dans `LibraryView`) — migrés quand même via
  `Content="{loc:Loc}"`, sinon l'EN afficherait du français : `Réinitialiser`, `Conseiller`,
  `Ouvrir` ×2, `Demander à l'IA`, `Annuler`.

**Notes techniques de la phase 4** (pour les phases 5 à 10) :
- **Littéraux techniques sur lignes françaises** : `" · "`, `"  ·  "`, `", "` (séparateurs de
  listes) et `"HWParamA_"`/`"HWParamB_"` (préfixes de paramètres matériels) étaient comptés
  parce que **leurs lignes** contiennent `Preset`/`preset` (détection `presets?`) ou
  `Select(preset => …)` — alors qu'aucun n'est du texte affiché. Traitement : **remontés en
  `private const` sur leurs propres lignes** (non françaises) → le scan ne les voit plus, la
  donnée est intacte. Réflexe pour les phases suivantes : *un littéral technique sur une ligne
  française se hoiste, il ne se migre pas*.
- **`"—"` de repli** (jamais compté) : 1 caractère, sous le seuil `≥ 2` du scan C# — laissé
  tel quel, ponctuation neutre FR/EN (ex. : `Folders`, `ChainSummary`).
- **Guillemets d'affichage** : `« {0} »` autour du titre de chanson est devenu une clé
  (`Detail.Chanson.Guillemets`) — la phase 10 pourra y mettre des guillemets droits en EN.
- **`POST`/`PRÉ`** (position de section, affichée par `PositionLabel`) : `PRÉ` étant français,
  les deux deviennent les clés `Detail.Position.Post`/`.Pre` (EN `PRE`).
- **Subtilité du dictionnaire `frRe`** : `mod[eè]le` n'accorde que `modèle`/`modele` — `model`
  et `ToneModelKind` **ne** déclenchent **pas** la détection ; `ToneModelDetailViewModel` ne
  comptait donc que 2 littéraux (le `", "` de la ligne lambda `preset`, et la phrase
  `Aucun preset n'utilise ce tone model.`).
- **Textes concaténés** : `SettingsNote` et `SettingsOriginNote` assemblaient deux littéraux —
  chaque paire est devenue **une clé unique** avec l'espace interne préservé verbatim
  (extraction G4 inchangée pour le FR).

**Notes techniques de la phase 5** (pour les phases 6 à 10) :
- **Piège des clés « françaises »** : en C#, une ligne portant une clé dont le **nom** contient
  un mot du dictionnaire (`Modele` → `mod[eè]le`, `Voix`, `Aucune`, `Chargement` → `charg`,
  `Cles` → `cl[ée]s`) est elle-même détectée comme française et la référence de clé devient un
  littéraux compté. Deux sorties : garder la clé **sur la même ligne que `Localizer.`**
  (ligne exclue par la règle) — c'est le cas de `Reglages.IA.ModelesTrouves`,
  `Reglages.Avis.VoixDisparues`, `Reglages.Bases.Aucune` — ou choisir un nom de clé sans
  mot déclencheur. Les clés `modeles`/`voix` en XAML sont indifférentes (la valeur `{loc:…}`
  commence par `{` et n'est jamais comptée).
- **Noms de tables techniques sur une ligne d'affichage** : `"Presets"` / `"ToneModels"` sont
  français (le texte `presets` est sur la même ligne du `CountsLabel`) — traités comme
  `HWParamA_` en phase 4 : appel `Localizer.Get` **en une seule ligne** incluant les noms de
  tables (exclusion `Localizer.`), plutôt que de migrer des identifiants de tables.
- **Non compté ≠ non affiché** (lacunes du dictionnaire `frRe`) : `Test avec {label}…`,
  `Format non reconnu` et le séparateur `{label} : {message}` échappaient au scan (ni accent ni
  mot-clé) mais sont du texte français visible en EN — **migrés quand même**. La phase 10 devra
  relire au-delà du scan (la sentinelle utile est la recette manuelle §14).
- **`Invite.Ping`** : l'invite « Réponds uniquement par le mot OK. » est sur une ligne
  française ; migrée en phase 5 avec une clé dédiée sous l'espace `Invite.` — la phase 7
  (invites Core paramétrées en langue, §8.1) pourra la reclasser sans changer d'appelant.
- **§8.4 soldé** : coloriage confirmé sur `IsFree`/`IsPaid` (jamais sur le texte) ;
  `Label`/`Tag` passent par `TierLabel` → `Model.Tier.Free`/`.Paid`, getters recalculés à
  chaque lecture. L'abonnement `CultureChanged` des libellés composés reste à câbler en
  phase 9 (§11) : `OpenCodeModel` est un `record` sans INPC, la bascule devra reconstruire
  les listes de modèles.
- **Marques et URL en clés d'identité** : `Gemini`, `AIza...`, `console.groq.com/keys`,
  `oc_sk_…`, `SHA-256` deviennent des clés à valeurs identiques FR/EN — le scan exige une
  clé (lettres dans l'attribut), le contenu est neutre.

**Notes techniques de la phase 6** (pour les phases 7 à 10) :
- **Protocole laissé à la phase 7** : `AdviceViewModel:347,405` et `AiOpinionRowViewModel:114`
  extraient les marqueurs que le modèle écrit (`BLOC :`, `VERDICT :`, `CONSEIL LIBRE`) — ce
  ne sont pas des textes affichés mais des motifs d'analyse : leur tolérance FR/EN est le
  contrat `AnswerFormat` (§9). Les deux fichiers restent donc dans l'allowlist, réduits à
  ces seuls littéraux (4 et 2) ; la phase 7 les fera sortir.
- **Course de culture entre tests (l'aléa de la phase)** : `SettingsDatabaseChoiceTests`
  (`Aucune base trouvée`) a échoué 2 fois en 25 exécutions en fin de phase 5 : le test
  construit `SettingsViewModel` (indice `Reglages.Bases.Aucune` localisé depuis la phase 5)
  pendant qu'un test de la collection `Localisation` tient le `Localizer` en `en-US` →
  `No database found…` à la place du FR. Cette collection ne sérialisait que les tests qui
  **bascule** la culture entre eux, pas ceux qui la **lisent** ; `AdviceViewTests` (clés de
  cette phase) et `DetailPanelTests` (phase 4) étaient dans le même cas, et chaque phase
  migrera d'autres lecteurs. Remède : `[assembly: CollectionBehavior(DisableTestParallelization
  = true)]` dans `tests\TonexAdvisor.Core.Tests\AssemblyInfo.cs` — la suite partage des
  singletons mondiaux (culture du `Localizer`, dispatcher Avalonia) → exécution sérialisée,
  12/12 vertes ensuite ; les tests ajoutés par les phases 7 à 10 sont sûrs par construction.
- **`Conseil.Avis.Titre` existait déjà** (« AVIS CROISÉS — OPENCODE, GEMINI, MISTRAL, GROQ »,
  titre statique du panneau, phase 1) : les titres dynamiques du VM ont pris
  `Conseil.Voix.Titre` (`AVIS DES VOIX`) et `Conseil.Voix.Suggestions`
  (`SUGGESTIONS DES VOIX`) — collision évitée, ordre des clés préservé.
- **`Plural` enfin branché** : `Usage` de la ligne combinaison appelle
  `Plural(PresetCount, "Conseil.Combination.Uses")` — les paires `.one/.other` existaient
  depuis la phase 1 sans consommateur — et `Uses.none` couvre le cas 0 : le `(s)` de cette
  ligne disparaît avant la passe pluriels de la phase 8 (qui traitera les sites restants).
- **Hoist `GenreSentinelle`** : la sentinelle de données `"None"` (`preset.Genre`) était sur
  une ligne française (`preset`) — remontée en `private const` sur sa propre ligne (réflexe
  de la phase 4) : la donnée est intacte et le scan ne la voit plus.
- **Non compté ≠ non affiché (bis)** : `Indiquez au moins un artiste…`, `Interrompu` et
  `en cours…` sortent des passes 3-4 de l'inventaire (ni accent ni mot-clé) — migrés quand
  même, en clés `Conseil.Erreur.RequeteVide`, `Conseil.Avis.Interrompu`, `Conseil.EnCours`.

**Notes techniques de la phase 7** (pour les phases 8 à 10) :
- **`AnswerFormat` est la seule source des marqueurs** (§9) : les six noms (`Bloc`, `Baffle`,
  `Reglages`, `Alternative`, `ConseilLibre`, `Verdict`) pour l'invite (langue de la session),
  `VoiceStarts`/`VerdictStarts`/`EndMarkers` pour l'extraction (marqueurs de la session
  d'abord, autre langue en repli), `AllMarkers` pour la coloration — qui peint **toujours les
  deux jeux**, quelle que soit la session : la coloration n'a pas de culture. Terminologie EN
  de §9 : `BLOC → BLOCK`, `BAFFLE → CAB`, `RÉGLAGES → SETTINGS`, `CONSEIL LIBRE → FREE
  ADVICE`, `VERDICT/ALTERNATIVE` identiques.
- **Invites Core : paramètre culture, défaut fr** — `CataloguePrompt.Build/DescribeCatalogue/
  AppendRules` et `ArbitrationPrompt.Build` prennent `CultureInfo? culture = null` (§8.1 : pas
  de `Localizer` dans Core ; règle `fr`→FR sinon EN, identique à §11). Le défaut `null` = fr :
  les tests Core français n'ont pas bougé ; l'App passe `Localizer.Instance.Culture` (prêt
  pour la bascule de la phase 9). Le gabarit de sortie est rendu depuis `AnswerFormat` :
  `« {format.Verdict} : »` produit en fr exactement l'ancien littéral.
- **Comptes qui montent** : les invites gagnent des littéraux (23 → 34, 18 → 29) parce que les
  branches EN vivent sur des lignes françaises (`format.Verdict` déclenche `VERDICT`, `«`
  déclenche…). Comptes honnêtes, miroir mécanique du scan ; le sort des invites en code FR/EN
  sera tranché en phase 10 (au même titre que le tableau en code de l'installeur, §11).
- **Graines `Marqueur.Bloc`/`Marqueur.Verdict`** des resx (phase 1) : plus la source de la
  coloration — `AnswerFormat.AllMarkers` a remplacé la table en dur d'`AnswerLineViewModel`
  (6 littéraux sortis). Les clés restent (consommées par `LocalizerTests`, parité) ; la phase
  10 décidera de les retirer ou non.
- **Hors périmètre 7** : `LibraryAdvisor` (25) et `StyleVocabulary` (7) ne portent ni invite
  ni marqueur d'extraction — l'algo du Conseil local n'a pas été touché (règle du cahier des
  charges) ; leur sort se joue dans les passes 8 à 10.

**Notes techniques de la phase 8** (pour les phases 9 à 10) :
- **`Plural` multi-arguments** : signature étendue en `Plural(int n, string baseKey, params
  object?[] args)` — `{0}` porte toujours `n`, `args` comble `{1}`… (ex. `Reglages.Bases.Trouvees`
  : compte **et** chemin). Les appels existants (2 arguments) sont inchangés.
- **Clés à deux nombres = colle + compteurs** : `Message.Resume.Bibliotheque` et
  `Reglages.Compteurs` combinent deux compteurs indépendants — impossible en une paire
  `.one/.other`. Leur valeur est réduite à la colle `{0} · {1}` (séparateur neutre, identique
  FR/EN) et chaque nombre vient de `Compteur.Presets.one/.other` / `Compteur.ToneModels.one/.other`
  (`{0:N0} preset` : le `N0` suit la culture du `Localizer` via `string.Format(_culture, …)`).
  La même colle porte la nouvelle ligne de statut : `Message.Resume.Chargement`
  (`{0} · {1} · {2}` = base, presets, tone models) + suffixes `Message.Resume.Jointe`
  (`{0}` = compteur pluriel) et `Message.Resume.NonDispo`.
- **Dates en `g`, jamais en motif** : `{1:g}` (date courte + heure courte de la culture) remplace
  `dd/MM/yyyy HH:mm` dans `Reglages.Fichier.Info` — en fr, l'afficheur est exactement l'ancien
  rendu (`05/10/2026 14:30`) ; en en-US il devient culture-conforme au lieu de l'ordre français.
  `LibraryFile.ModifiedLabel` (Core) fait de même : `ToString("g", CultureInfo.CurrentCulture)`.
- **Unité de taille = règle §11 en Core** : `LibraryFile.SizeUnit` (« Mo » si culture `fr`,
  « MB » sinon) reprend le mécanisme des invites de phase 7 — règle en code, pas de `Localizer`
  dans Core. La clé `Reglages.Fichier.Info` garde, elle, son `Mo`/`MB` par fichier de langue.
- **Clé posée sur la ligne `Localizer.`** : une clé seule sur sa ligne compte quand sa ligne
  matche `frRe` (`Message.Resume.Chargement` → « charg », `…Jointe` → « joint », `…VoixDisparues`
  → « voix », `database.Count("Presets")` → « presets »). Réflexe des phases 4/6 : premier
  argument sur la même ligne (`Get("clé",` / `Plural(…, "clé",`) — sinon l'allowlist fausse.
  Contrôle : scan sans écart, `MainViewModel` 11 → 7 (4 littéraux de statut migrés, 0 ajout),
  `SettingsViewModel` reste hors allowlist.
- **Mises à jour conscientes de tests** : `VoiceAndModelTests` assertait « ne sont plus
  disponibles » — le `(s)` forçait le pluriel même à 1 voix ; l'accord réel est désormais
  « n'est plus disponible » (singulier fr : 0 et 1). `LocalizerTests.
  Formatage_des_nombres_suit_la_culture` recompose `Message.Resume.Bibliotheque` via `Plural`
  : son objet (le `:N0` calé sur la culture) est intact, seul l'affichage a changé.
- **Reste en `(s)`** : `LibraryAdvisor:133` (« Utilisé par {used} preset(s) ») — raison du
  Conseil construite en Core, hors périmètre 8 comme les 25 littéraux de `LibraryAdvisor` :
  traitée à la phase 10 (allowlist vide).

**Notes techniques de la phase 9** (pour les phases 10 à 11) :
- **Détection puis choix** : `UiLanguages.Resolve(état, CurrentUICulture)` — « fr » → fr, tout
  le reste → en (décision 3) ; appelée par `Program.FrameCulture()` **avant l'UI**, avec
  écriture de `UiLanguage` (IOException/UnauthorizedAccess tolérés : un disque plein
  n'empêche pas de démarrer). `SettingsViewModel` relit au montage (tests, fichier vierge)
  mais n'écrit qu'un choix **explicite** ; l'aller-retour fr→en→fr persiste le dernier.
- **Compositeurs = la mécanique de la liste fermée §11.4** : `SetStatus`, `SetVoicesTitle`,
  `SetAiTitle`, `SetAiStatus` gardent le dernier `Func<string>` ; la bascule le rejoue avec les
  arguments d'origine — **sans rejeter de requête IA**. Les 3 sites en dur de `MainViewModel`
  (« Choisissez un fichier… », « Lecture de… », « Échec : ») recomposent aussi, mais restent
  FR jusqu'à leurs clés de phase 10.
- **Abonnements permanents, conséquence de test** : les 4 VM s'abonnent à `CultureChanged` et
  ne se désabonnent jamais (précédent phase 1) — un handler survit à son test et se rejoue à
  la culture suivante. Le test de statut laisse donc sa base V2 **ouverte** : la refermer
  ferait échouer les bascules des tests voisins (`ObjectDisposedException` constaté).
- **Détail reconstruit, pas re-notifié** : `RebuildDetail()` crée un nouveau détail (chaînes
  composées datent de la création : `SettingsNote`, notes de position, `PresetsNote`) et
  reprend `ShowHardware`. Même raisonnement pour les listes de filtres (§11.6) et les cellules
  du sélecteur de modèles : **un gabarit ne se revalide jamais**, seule la notification fait
  repeindre — `VoiceChoiceViewModel.NotifyLocalizedLabels()` notifie `Label`/`Tag` sans
  reconstruire la liste (les cases cochées survivent).
- **§8.2 en deux couches, un piège attrapé par les tests** : `ColumnIds` bâtit, par grille, un
  index id ↔ clé ↔ en-tête FR ↔ en-tête EN — l'en-tête historique entre dans l'index par
  `Localizer.GetForCulture(key, culture)`, **indépendant de la culture courante**. Piège : la
  table doit ranger la **clé** (`Biblio.Col.Nom`) et non la valeur localisée du jour — ranger
  la valeur excluait les clés EN de l'index : tests FR verts, tests EN rouges (c'était le
  signal). La vue ne voit que des ids : sauver = `IdOf(header)`, appliquer = `HeaderOf(clef)`
  comparé à l'en-tête courant — traduit dans les deux sens, stable dans les deux langues.
- **Lignes de table = références de clé** : `("name", Key(Localizer.Instance, "Biblio.Col.Nom"))`
  — la ligne contient `Localizer.` → exonérée du scan G6 (un en-tête de colonne n'est pas un
  littéral d'affichage) et `Key()` **valide** la clé chez le Localizer (`Debug.Fail` en debug
  si elle a bougé). Allowlist donc **inchangée : 26 fichiers / 194 littéraux**.
- **Mises à jour conscientes de tests** : `PersistenceTests` (3) — en-têtes FR (`Nom`,
  `Stomp`, `Ampli`) → ids (`name`, `stomp`, `amp`) ; `RestoringColumnWidths_NotifiesTheViewOnce`
  sert désormais de preuve §8.2 « blob FR charge, se traduit, notifie une fois ». `UserStateTests`
  : aller-retour `UiLanguage` ajouté au round-trip + **blob v1.2.1 à 12 champs** (sans
  `UiLanguage` → `null` = jamais choisi) + `Clone()` qui reprend la langue.
- **Expressions à figer avant la bascule** : `SettingsNote`, `TonexFolderHint`, `Label` sont
  des `=>` qui se recomposent **à chaque lecture** — comparer avant/après sans passer par un
  local revient à comparer deux fois la nouvelle langue (piège relevé en cours de phase :
  « Strings are equal » avec le texte EN des deux côtés).
- **§11.6 soldé côté VM** : `Fill` vide puis re-remplit les trois listes (événement `Reset` à
  chaque bascule, sélection capturée/restaurée) — c'est ce `Reset` que le test observe, la
  sentinelle affichée repartant du `FilterLabelConverter` dans la langue courante.

**Notes techniques de la phase 10** (pour la phase 11 et au-delà) :
- **Décision §17 n°5 — une paire de ressources Core** : `CoreTexts` (sans dépendance au
  `Localizer` de l'App, §8.1) lit `src\TonexAdvisor.Core\Localization\Strings{,.fr}.resx`
  (180 clés, `NeutralResourcesLanguage=en` ajouté au csproj). La langue entre **par paramètre**
  pour les invites (les tests FR d'invites sont restés verts sans modification : extraction
  verbatim des deux branches du code) et par la **culture ambiante** (cadrée par l'App, pinée
  par `CultureGuard`/`TestCulture`) pour les raisons, les potards et l'origine de jointure.
  Le satellite `fr\TonexAdvisor.Core.resources.dll` double celui de l'App : la recette §14
  (« dossier fr\ présent ») porte désormais sur les **deux** DLL.
- **Décision §17 n°6 — le scan affiné, mesuré avant d'être coupé** : les huit marqueurs
  retirés (`presets?`, `Favori/favori`, `Tous`, `BLOC`, `ALTERNATIVE`, `Tonex`, `tone model`,
  `mod[eè]le` — le « ModelE » de `ModelEnable` mordait le motif) ne servaient qu'au hors-champ :
  SQL `Presets`/`Favorite`, chemins et user-agents « Tonex », « TextBlock » pris pour BLOC,
  sentinelle « Tous » (§8.3 : sa valeur est **restée** « Tous », c'est le scan qui a bougé),
  vocabulaire de style, identifiants `ModelEnable`. Mesure faite sur l'inventaire : **zéro
  ligne d'affichage** ne dépendait d'eux seuls. Le miroir `tools\inventaire-i18n.ps1` a suivi.
- **Exemption des deux moteurs de clé** : la règle « une clé n'est pas une chaîne
  d'affichage » s'applique aux lignes contenant `Localizer.` **ou** `CoreTexts.` — sans quoi
  une clé Core portant un mot de protocole (`…Regles.Bloc`, `…Baffle`) serait comptée comme du
  texte. Piège de code rencontré : factoriser l'appel dans un alias local (`Raison(...)`)
  **retire** l'exemption (la ligne ne porte plus `CoreTexts.`) — chaque site appelle le moteur
  directement.
- **Allowlist vidée, garde reformulée** : `Assert.NotEmpty(autorise)` (« l'allowlist livrée
  n'est pas vide ») serait devenu faux — remplacé par « le fichier **existe**, même vide » ;
  le fichier ne contient plus qu'un commentaire de phase. Toute chaîne qui se re-remplit le
  fait échouer (G6).
- **`Potard.*` : la clé comme donnée, le texte à la lecture** : `KnobDefinition.LabelKey`/
  `SettingSection.TitleKey` + propriétés calculées `Label`/`Title` — les libellés ne sont
  **jamais figés** au démarrage (une valeur composée en `static readonly` serait restée dans
  la langue du premier affichage). 106 clés extraites de l'inventaire par script (FR = valeur,
  EN = statut « EN = … », sinon identique) ; `Potard.Cab.Baffle` renommée
  **`Potard.Cab.Model`** — le mot « Baffle » dans une clé re-déclencherait le scan (`BAFFLE`).
- **Raisons composées en culture ambiante** : `CoreTexts.Format(key, null, args)` — la raison
  se fige à l'instant de l'`Advise()` ; passer en anglais puis relancer le Conseil produit des
  raisons anglaises, et un conseil **déjà affiché** reste dans sa langue (recette §14, point 2
  : « conseils IA déjà affichés : inchangés, normal »). Le `(s)` de `LibraryAdvisor:133`
  (report phase 8) est soldé : `Raison.Uses.one/.other` via `CoreTexts.Plural` (règles
  fr/en en miroir du `Localizer`).
- **Origine de jointure = propriété, pas constante** : `SettingsJoin.Origin` est écrit dans
  les enregistrements **en mémoire** uniquement (les bases ne sont jamais ouvertes en
  écriture) — la changer en `static string => CoreTexts.Get(...)` la compose dans la langue
  de la session de jointure sans toucher au contrat « lecture seule ».
- **« Non compté ≠ non affiché », seconde vague** : `Erreur.IA` (6 sites), `canal`,
  `Rig complet` et les autres types de tone model échappaient au scan (ni accent ni mot-clé)
  mais restaient français en EN — inventaire pris au mot ; idem pour les `Debug.Fail` du
  `Localizer` passés en anglais (messages dev, guillemets droits : les `« »` eux-mêmes
  comptaient comme marqueur français !).
- **Nuance recette — lignes de grille** : les rangées (dont le libellé « Rig complet ») sont
  reconstruites au **prochain filtre/chargement**, pas à la bascule (reconstruire 2 500 rangées
  à chaque changement ferait tomber la sélection ouverte — arbitrage aligné sur la liste
  fermée §11.4, qui ne cite ni les rangées ni leur contenu). En-têtes, résumé, détails,
  réglages et statuts basculent, eux, instantanément.
- **Artefacts de vérification** : `artifacts\TonexSongAdvisor-1.2.1-portable.zip` (+ `setup.exe`)
  régénérés par `tools\build-release.ps1` — les deux satellites `fr\` y sont présents (le
  fichier `payload.zip` de l'installateur est l'archive portable elle-même). La vérification de
  la **langue de la console** de l'installeur appartient à la phase 11 (G10), la signature de
  la recette §14 à vous.

**Méthode d'inventaire (phase 0)** — 4 passes de scan :
1. XAML : tous les littéraux d'attributs texte, **sans filtre de langue** (126 lignes) ;
2. C# : lignes francophones (accents + mots-clés) → 397 candidats bruts ;
3. complément : littéraux contenant une espace, absents du brut ;
4. complément : mots isolés (initiale majuscule, puis minuscule).

Les passes 3 et 4 ont découvert ~40 candidats oubliés par le scan francophone — ex. :
`Indiquez au moins un artiste…`, `Interrompu`, `en cours.`, `Erreur IA (…)`, les libellés
`KnobCatalog` déjà anglophones, les noms de voix affichés (`OpenCode Go`, `Gemini (Google)`).
Contrôles automatiques passés : chaque ligne du brut est couverte dans le curaté
(fichier:ligne), statuts conformes à la taxonomie, zéro champ invalide.

**Critère de sortie de la phase 0** : inventaire complet validé (≥ 250 clés) →
**388 clés uniques sur 475 lignes à migrer** ✅ — baseline figée 195/195 ✅.

---

*Plan validé ; phases 0 à 10 réalisées sur instruction. Prochaine étape : **phase 11**
(installeur FR/EN : table en code, console dans la langue de l'OS, §12/G10), à lancer sur
instruction. Recette §14 prête — **signature de votre part** avant clôture.
Phase 10 non committée (dernier commit : `537b3c7`, phase 9).*
