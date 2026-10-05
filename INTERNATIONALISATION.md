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
   (`SettingsView.axaml:100-101,160`), pas par le texte. Seul le **libellé**
   (`OpenCodeModels.cs:8` `$"{Id}  ·  gratuit"`) est à traduire ; la propriété
   `Tag => "gratuit"` (`:11`) est à auditer (voir §8.4).

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
| **5 — Réglages (UI + VM)** | `SettingsView.axaml`, `SettingsViewModel` (le plus gros : 32 lignes accentuées), badges §8.4 | idem |
| **6 — Avis IA (UI)** | `AdviceViewModel` (titres, spinner, états), `AiOpinionRowViewModel`, lignes de réponse | idem |
| **7 — Protocole IA (le plus délicat)** | `AnswerFormat` unique, invites Core paramétrées en langue (§8.1), marqueurs tolérants (§9), `AnswerLineViewModel` | Tests d'extraction/coloration FR **et** EN verts ; 16 tests français verts non modifiés |
| **8 — Passe nombres/dates/pluriels** | Les 33 sites de formatage, élimination de `(s)` | Plus aucun format en dur ; tests verts |
| **9 — Langue : détection, choix, bascule** | `UiLanguage` + rétrocompat (§10), ComboBox réglages, recalcul à chaud (§11), largeurs de colonnes en ids (§8.2) | Tests persistance + compat + bascule verts |
| **10 — EN complet + recette** | `Strings.resx` (neutre EN) et `Strings.fr.resx` remplis à 100 % (parité = 100 %), allowlist = **vide**, vérification satellite `fr\` dans le zip et l'installeur, recette manuelle des deux langues (§14) | Parité 100 %, scan sans allowlist, recette signée |
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
| **G5** | FR **et** EN complets et cohérents (EN = ressource neutre, maillon de repli final) | parité de clés, non-vidité, parité des placeholders `{0}` | test de parité (phase 2) |
| **G6** | Aucune nouvelle chaîne en dur | scan de littéraux XAML/C# avec allowlist par fichier → **vide** en phase 10 | test scan (phase 2) |
| **G7** | Protocole IA intact dans les deux langues | `AnswerFormat` unique + tests d'extraction/coloration croisés FR/EN | `LocalizationTests` / `AnswerCleanerTests` étendus |
| **G8** | État utilisateur préservé | tests : ancien `state.json` sans `UiLanguage` ; clés de colonnes FR historiques converties ; aller-retour filtres inchangé ; `Clone()` complet | `PersistenceTests`/`UserStateTests` étendus |
| **G9** | Bascule à chaud complète | test unitaire : changer `Culture` → `PropertyChanged("Item[])"` + `CultureChanged` + recalcul des chaînes composées listées en §11 | `LocalizationTests` |
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

Aucune question bloquante restante : les 4 décisions ci-dessus sont intégrées dans le plan.

---

## 18. Suivi d'exécution

| Date | Étape | État | Preuve / artefacts |
|---|---|---|---|
| 04/10/2026 | Plan + décisions (§17) | ✅ fait | ce document, `README.en.md` |
| 04/10/2026 | **Phase 0 — Baseline + inventaire** | ✅ fait | baseline : `dotnet build` 0 erreur, **195/195 tests verts** ; inventaire : `inventaire-i18n.csv` — **566 lignes**, dont **475 à migrer** (1 sentinelle §8.3) et **91 hors-champ**, **388 clés uniques** ; générateur `tools\inventaire-i18n.ps1` ; brut `inventaire-i18n-raw.csv` (397) conservé pour contrôle de dérive |
| 04/10/2026 | **Phase 1 — Socle (`Strings.resx` + `Localizer`)** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **204/204 tests verts** (195 inchangés + 9 nouveaux socle), **12/12 exécutions consécutives vertes sur le code final** (36/36 pendant la stabilisation, cf. notes dispatcher ci-dessous) ; artefacts : `src\TonexAdvisor.App\Localization\{Strings.resx, Strings.fr.resx, Localizer.cs, LocExtension.cs}`, cadrage culture `Program.FrameCulture` (fr statu quo), `<NeutralResourcesLanguage>en</NeutralResourcesLanguage>`, tests `TestCulture` (ModuleInitializer fr-FR), `CultureGuard`, `LocalisationCollection`, `LocalizerTests` (7), `LocExtensionTests` (2) |
| 04/10/2026 | **Phase 3 — Fenêtre principale + bibliothèque** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **209/209 tests verts** (aucun ajout : 204 socle/garde-fous + 5 de phase 2), **12/12 exécutions consécutives vertes** ; sortie d'allowlist : `MainWindow.axaml` **11 → 0**, `LibraryView.axaml` **53 → 0**, `LibraryViewModel.cs` **5 → 1** (sentinelle §8.3 seule) ; **59 clés FR/EN** ajoutées en paires ordonnées (**71 au total**, parité vérifiée) ; sentinelle : valeur `AnyFilter` intacte + affichage localisé (`FilterLabelConverter`, 3 ComboBox, §8.3) ; artefacts : `xmlns:loc` + `{loc:Loc}` (2 XAML), `Localizer.Instance.Get/[]` (VM), `src\TonexAdvisor.App\Localization\FilterLabelConverter.cs`, allowlist régénérée **38 fichiers / 332 littéraux** |
| 05/10/2026 | **Phase 4 — Vues de détail** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **209/209 tests verts** (aucun ajout), **12/12 exécutions consécutives vertes** ; sortie d'allowlist : `PresetDetailView.axaml` **13 → 0**, `ToneModelDetailView.axaml` **12 → 0**, `PresetDetailViewModel.cs` **15 → 0**, `ToneModelDetailViewModel.cs` **2 → 0** ; **27 clés `Detail.*`** ajoutées en paires ordonnées (**98 au total**, parité vérifiée) ; allowlist **34 fichiers / 290 littéraux** ; artefacts : `xmlns:loc` + `{loc:Loc}` (2 vues), `Localizer.Instance.Get/[]` (2 VM), constantes techniques `Sep`/`SepLarge`/`PrefixHwA`/`PrefixHwB` |
| 04/10/2026 | **Phase 2 — Garde-fous (parité + scan)** | ✅ fait | `dotnet build` **0 erreur / 0 avertissement** ; **209/209 tests verts** (204 + 5 nouveaux), **12/12 exécutions consécutives vertes** ; garde-fou G6 **prouvé par la sonde** : ajout d'une chaîne en dur (`SondeScanTemporaire.cs`) → test rouge « hors allowlist », sonde retirée → vert ; artefacts : `PariteResxTests` (3 tests : clés, non-vidité, emplacements `{0}`), `ScanLitterauxTests` (2 tests : compteur exact vs allowlist + entrées périmées), `tests\TonexAdvisor.Core.Tests\litteraux-allowlist.txt` (**34 fichiers / 344 littéraux**), `TestPaths.RepoRoot` (racine via `TonexAdvisor.sln`) |

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
  d'impact de culture — conversion site par site en phase 8.
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

*Plan validé ; phases 0 à 4 réalisées sur instruction. Prochaine étape : **phase 5**
(réglages UI + VM : `SettingsView.axaml`, `SettingsViewModel`, badges §8.4 — le plus gros
fichier du lot, 32 lignes accentuées, §12), à lancer sur instruction.
Aucun commit effectué.*
