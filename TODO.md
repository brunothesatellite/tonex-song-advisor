# TODO — Tonex Song Advisor

Version **1.0** : lecture V1/V2, conseil local, avis croisés multi-IA, installeur et portable.
Ce document liste ce qui reste à faire.

---

## ✅ Ce qui est fait

- **Squelette** : solution `TonexAdvisor.sln` (Core / App / Tests), `net9.0`, warnings = erreurs.
- **Lecture 100 % lecture seule** des bases TONEX :
  - triple barrière : `Mode=ReadOnly` + `Pooling=False` + `PRAGMA query_only=ON` + filtre verbale SQL
    (`SELECT`/`WITH`/`EXPLAIN` uniquement) ;
  - empreintes SHA-256 capturées à l'ouverture et revérifiées à la fermeture (`VerifyUnchanged`) ;
  - formats V1 (`db\Library.db`, réglages numériques complets) **et** V2 (`db\Library2.db`).
- **Navigateur** : 2 310 presets, 3 097 tone models, recherche, filtres catégorie/genre/dossier,
  favoris, réinitialisation, 2 onglets, panneau de détail (métadonnées, tonemodel, potentiomètres
  en lecture seule, sections matériels masquables).
- **UI** thème sombre « rock/metal » (`Themes/DarkRock.axaml`), DataGrid, disposition fixe
  (`DockPanel` : grille à gauche, carte de détail 400 px à droite).
- **Tests** : 169 tests verts (données, readers V1/V2, tokeniseur, ranges, panneau de détail, filtres,
  configuration IA).
- **Barre de titre Windows** en mode sombre (`DwmSetWindowAttribute`).

### Corrections récentes (dernier cycle)
- Le glyphe `★` (U+2605) n'existe pas dans Inter → **jamais rendu**. Remplacé par une étoile
  **vectorielle** (`Path`, style `Path.favStar` / `.favOff`) dans les cellules, les favoris,
  les cartes de détail et le bouton « Favoris ».
- Colonne « Réglages » et colonne « Fav » élargies (44 → 64/88 px) : sous ~44 px l'en-tête de
  colonne était rogné à zéro pixel et disparaissait.

### Cycle suivant — réglages IA (fait)
- **Clé API stockée hors du dépôt** : `%APPDATA%\TonexAdvisor\config.json` (`AppConfig`), avec
  `.gitignore` qui refuse aussi `config.json`, `*.config.json`, `*.secrets.json`, `.env*`.
- **Client OpenCode** (`Services/OpenCodeClient.cs`) : `Bearer` + `x-opencode-session` +
  `User-Agent: TonexAdvisor/1.0`, repli sur `reasoning_content` quand `content` est nul.
- **Modèles gratuits uniquement** (`Config/OpenCodeModels.cs`) : la liste est verrouillée sur les
  seuls modèles « Free » de Go — `longcat-2.5-preview-free` (défaut) et `space-bunny-free`.
  Le free tier **Personnel** (MiMo-V2.6-Flash Free, Ling, Nemotron…) refuse toute clé API avec
  `403 FreeTierError : OpenCode's free tier can only be used from within OpenCode` : il n'est
  utilisable que depuis l'application OpenCode (session OAuth), donc écarté volontairement.
- Carte **« Conseil IA »** dans Réglages (clé masquée, choix du modèle, *Tester la connexion*),
  remontée au-dessus de la ligne de flottaison — vérifié par automatisation UI (UIA) :
  `Connexion OK avec LongCat 2.5 Preview Free : « OK »`.

### Cycle : choix de la base de données (fait)

- [x] Deux choix dans « Bases & réglages » : le **chemin manuel** (comme avant), ou la case
      « Utiliser une base du dossier TONEX » + une **liste des bases du dossier officiel**.
- [x] Dossier trouvé par le *Known Folder* « Documents » de Windows — correct même quand
      Documents est redirigé vers OneDrive. Chaque ligne : nom, génération V1/V2, taille, date.
- [x] Case cochée : le champ de chemin et « Ouvrir » sont **grisés**, la base choisie se charge
      immédiatement. Case décochée : retour au chemin, qui est **rechargé** sans clic.
- [x] Choix **mémorisé** dans `state.json` (mode, chemin, base sélectionnée) — retrouvé au
      redémarrage.
- [x] Correctif : le mode TONEX n'écrase plus le chemin du choix 1 (le rechargement recopiait le
      chemin de la base ouverte dans le champ, qui était perdu au décochage).
- [x] Les deux écrans lisent-modifient-écrivent le même `state.json` sans s'effacer mutuellement.
### Phase 3 - Recommandation locale (fait)
- [x] `LibraryAdvisor` dans `TonexAdvisor.Core` : note presets et blocs capturés pour une
      chanson, un artiste ou un style (requête libre, aucun champ obligatoire).
- [x] Signaux combinés : catégorie/saturation (`HI-GAIN`, `DRIVE`, `CLEAN`, `STOMP - …`), genre,
      artiste, chanson, mots-clés/dossiers/auteur, favoris, réglages exploitables. Le gain
      numérique (`ModelGain`) a été testé puis écarté : médiane à 5 partout, il ne dit rien.
- [x] Classement « meilleur preset » + « bloc capturé (stomp + ampli) + baffle ».
      **Règle respectée** : le stomp et l'ampli d'une capture sont inséparables — jamais de
      mélange entre deux tone models — et **seul le baffle se change**, choisi dans toute la
      bibliothèque avec la mention « remplaçable par tout autre baffle ».
- [x] Explication lisible de chaque conseil : « Catégorie HI-GAIN : saturation adaptée à « metal » »,
      « Baffle « M-Tech Audio » : saturation adaptée à « metal » », « TS808 devant l'ampli… ».
- [x] Onglet « Conseils » : formulaire artiste/chanson/style, 3 meilleurs presets et la
      combinaison, chacun avec son score, ses raisons et un bouton « Ouvrir » qui sélectionne
      la ligne dans la grille.
- [x] Vérifié par automatisation UI (UIA) : saisie « metal » → 3 presets HI-GAIN (92/92/84 %) et
      le bloc `Ibanez Tube Screamer TS808 -> Mesa Boogie Triple Rectifier` + baffle libre.
### Phase 4 - Conseil IA via OpenCode Go (fait)
- [x] Client HTTP : `https://opencode.ai/zen/go/v1/chat/completions` (OpenAI-compatible,
      `Authorization: Bearer`, `x-opencode-session`, `User-Agent: TonexAdvisor/1.0`).
- [x] Saisie de la clé API + stockage local (`%APPDATA%\TonexAdvisor\config.json`, hors repo) et
      choix du modèle **gratuit** uniquement, avec test de connexion réel.
- [x] Streaming SSE + affichage progressif du conseil (`SseParser`, plusieurs schémas de delta
      acceptés : `content`, `text`, `message`). La chaîne de pensée s'affiche pendant la
      génération puis disparaît dès que la réponse arrive.
- [x] Contexte envoyé au modèle : la demande + les 3 presets les mieux notés et le bloc capturé
      (`AdvicePrompt`) — jamais toute la bibliothèque, et avec la règle « stomp + ampli
      inséparables, seul le baffle se change ».
- [x] Gestion d'erreur : clé absente, hors-ligne, clé invalide, quota dépassé, annulation —
      message explicite et **repli sur le classement local**, qui reste affiché.
- [x] Bouton « Demander à l'IA » branché à l'écran « Conseils », vérifié en réel par UIA :
      réponse française en ~50 s (`Conseil IA — LongCat 2.5 Preview Free`).
### Phase 5 — Fiabilité et finitions (fait)
- [x] Test d'intégrité **automatisé** : hash des 2 bases avant/après une session complète
      (lecture, conseil, clics dans l'IU) + aucun journal SQLite ni pages en attente.
- [x] Persistance des filtres et de l'onglet actif (`%APPDATA%\TonexAdvisor\state.json`, hors dépôt).
      Les favoris viennent de TONEX lui-même : rien à persister, et les bases ne sont jamais écrites.
      (vérifié par automatisation UI, colonne lue avant/après).
- [x] Colonne « Réglages » : la coche (U+2714) n'existe pas dans Inter, comme l'étoile (U+2605)
      avant elle : remplacée par un `Path` vectoriel (`Path.checkGlyph`), affichée dans les deux sens.
- [x] Sélection de ligne aux couleurs du thème : fond orange translucide + liseré accent.
- [x] Icône d'application (`Assets/app.ico`) + `ApplicationIcon`, barre de titre en mode sombre.
      Reste : le thème de la barre des tâches est un réglage Windows, hors application.
      230 fichiers / 206 Mo, commande documentée dans le README). Reste : un vrai installeur.
- [x] CI GitHub Actions (`.github/workflows/ci.yml`) : build + tests à chaque modification.
      Les tests dépendants des bases (jamais versionnées) sont ignorés automatiquement
      (`[LibraryFact]`), la CI vérifie le reste.

### Phase 6 - Avis croises multi-IA (fait)
Objectif : ne plus s'en tenir a un seul modele. Croiser l'avis d'**OpenCode** (deja branche), de
**Gemini (gratuit)** et de **Copilot (gratuit)**, puis faire arbitrer les trois avis pour ne
garder que les **3 meilleures propositions**. Gemini et Copilot sont explicitement invites a
**contester** la recommandation de reference (preset, bloc capture, reglages), pas a la
confirmer : c'est le desaccord qui fait la valeur de ce mode.

- [x] Interface commune `IAiClient` (`AskStreamAsync`), implementee par `OpenCodeClient` (reference)
      et par `OpenAiCompatClient`, un seul connecteur pour tous les fournisseurs compatibles OpenAI.
- [x] Connecteurs **Gemini**, **Mistral** et **Groq** (`Config/AiProviders.cs`) : endpoints
      compatibles OpenAI, modeles gratuits par defaut, liens des pages de demande de cle dans le
      README et dans les reglages.
- [x] **Copilot ecarte** : pas d'API publique gratuite. Remplace par **Mistral** et **Groq**,
      qui ont de vrais quotas gratuits avec une cle utilisable depuis une application tierce.
- [x] Consultation **en parallele** (`AiConsultation`) : meme contexte `CataloguePrompt` (catalogue complet, noms seuls), delai de
      60 s par voix, chaque echec/depassement devient l'avis de cette voix et n'arrete rien.
- [x] **Confrontation sans ancrag** : les voix repondent independamment (pas de reponse de
      reference a commenter, cela evite de les aligner dessus), puis `ArbitrationPrompt` demande
      a l'arbitre de relever ce qui cloche chez chacun.
- [x] **Arbitrage** : tour de synthese qui classe les 3 meilleures propositions, les points de
      desaccord et le niveau de consensus (3/3, 2/3, 1/3), jamais hors de la selection locale.
- [x] IU : carte « Avis croises » sur l'onglet Conseils (un bloc par voix, avec son delai ou son
      erreur) + la synthese arbitree, et saisie des cles Gemini/Mistral/Groq dans les reglages.
- [x] Repli : un fournisseur en echec ou en timeout est signale ; a 2 voix ou plus l'arbitrage
      tourne, a une seule voix on affiche son avis, a zéro on garde le classement local.
- [x] Tests : connecteurs factices (succes, erreur, timeout), selection des voix d'apres les
      cles renseignees, arbitrage a partir d'avis ecrits, catalogue des fournisseurs.
### Phase 7 - Réglages des potards en génération 2 (V2) (fait)

**Analyse faite (lecture seule, bases + dossier TONEX) — résultats :**

1. **Les valeurs ne sont pas dans `Library2.db`.** `Presets` V2 = 20 colonnes, dont `Chain` (TEXT)
   qui ne contient qu'une liste de blocs : `[{"ID":0,"Bypass":false}, ...]` — aucun paramètre.
   Confrontation sur un preset présent dans les deux bases : `ModelGain=2.3`, `EqTreble=5.9`,
   `PwrAmpEqPresence=7.1` (V1) n'apparaissent nulle part dans le Chain V2.
2. **Elles vivent dans les fichiers `.txp`** du dossier TONEX :
   `\Library\Presets\*.txp` (2 513 fichiers = exactement le nombre de presets V2) et
   `\Library\ToneModels\*.txm` (3 714 = les tone models V2). Ces fichiers sont **chiffrés**
   (en-tête `29832.<base64>`, aucun mot lisible) — même signature que les champs `*Encrypt` de la
   base. Les décrypter = reverse engineering du format propriétaire d'IK : hors sujet.
3. **Ce qui est récupérable légitimement : la jointure V1 ↔ V2.** Ta base V2 est un upgrade de la
   V1 : **2 489 presets V2 sur ~2 513 existent en V1**, où les valeurs sont en clair.

**Implémenté :**
- [x] Quand une base V2 est ouverte et qu'une base V1 est disponible, enrichir les presets V2 avec
      les réglages V1 (jointure par nom de preset, secours par GUID du tone model).
- [x] Afficher les potentiomètres pour ces presets, avec la mention honnête « réglages issus de la
      bibliothèque V1 — les valeurs V2 sont chiffrées dans les fichiers .txp ».
- [x] Les ~200 presets présents seulement en V2 restent sans réglage (valeur chiffrée, non lisible).
- [x] Tests : jointure sur les deux bases (2 313 presets enrichis), non-régression V1 et V2, hash des bases inchangé. Règle : la jointe ne remplit que ce qui manque, une base V1 garde ses propres valeurs.

---

## 🔜 Reste à faire

### Phase 8 - Internationalisation (français / anglais)

Objectif : l'application en français **ou** en anglais, la bonne langue détectée au premier
démarrage, le choix dans les réglages, et une architecture qui accepte une troisième langue sans
retravailler les écrans.

**État de départ :** toutes les chaînes sont en dur en français — XAML (libellés, colonnes,
infobulles, boutons) et ViewModels (messages d'état, erreurs, notes, formats de réponse IA).
Quelques tests assertent sur des textes français.

1. **Mécanisme de ressources** (le socle, en premier) :
   - un fichier de ressources par langue (`Strings.fr`, `Strings.en`), clé → texte ;
   - un petit service `Localizer` exposé en ressource d'application, avec un indexeur
     (`{Binding [Cle]}`) et une propriété `Culture` : changer de langue lève `PropertyChanged` et
     **toute l'IU se met à jour sans redémarrage** ;
   - aucune chaîne littérale ne reste dans un écran : tout passe par une clé.
2. **Inventaire** (le gros du travail, ~150 à 250 chaînes) : XAML, ViewModels, messages d'erreur,
   infobulles, colonnes de grille, notes du panneau de détail, titres de cartes, texte des
   invites IA. Une seule source de vérité par phrase.
3. **Détection au premier démarrage** : `CultureInfo.CurrentUICulture` — français si la langue du
   système est le français, sinon anglais. Le résultat est écrit dans `state.json` à la première
   exécution ; ensuite **le choix de l'utilisateur gagne**.
4. **Choix dans les réglages** : liste « Langue » (Français / English) à côté de la base de
   données, mémorisée comme les autres préférences, effet immédiat.
5. **Évolutivité** : la liste des langues vient des **fichiers de ressources présents**, pas d'une
   liste en dur — ajouter une langue = ajouter un fichier ; le sélecteur et la détection suivent
   seuls.
6. **Points spécifiques à trancher pendant l'implémentation** :
   - la **langue des réponses IA** suit celle de l'interface (le prompt dit aujourd'hui « réponds
     en français ») ;
   - les **marqueurs de format** (`BLOC`, `BAFFLE`, `RÉGLAGES`, `ALTERNATIVE`, `CONSEIL LIBRE`,
     `VERDICT`) servent d'ancres au nettoyage des réponses (`AnswerCleaner`) : soit on les garde
     fixes quelle que soit la langue, soit on les traduit **et** on utilise le marqueur localisé
     dans le nettoyage — à décider une fois pour toutes ;
   - formatage des nombres et dates selon la culture (séparateur décimal, `1,5 s` / `1.5 s`).
7. **Tests** : complétude des ressources (toutes les clés dans toutes les langues, ni plus ni
   moins), détection (système fr → français, sinon anglais), changement de langue pris en compte
   sans redémarrage, marqueurs de format intacts, non-régression des écrans.
8. **Hors périmètre pour l'instant** : traduire le README / le manuel, langues RTL (arabe,
   hébreu) — à réexaminer si une telle langue est ajoutée.
### Autres évolutions demandées

- [ ] **Colonnes redimensionnables à la main** : la mécanique existe déjà (`CanUserResizeColumns`
      sur la grille, `CanUserResize` par colonne, poignée en bord d'en-tête), mais les largeurs
      en `*` (`1.7*`, `1.3*`…) se partagent l'espace et **reprennent la main** quand la fenêtre
      change de taille, ce qui annule le redimensionnement. Trois niveaux possibles :
      1. forcer `CanUserResizeColumns="True"` (une ligne) ;
      2. largeurs en **pixels fixes** + barre de défilement horizontale — prévisible, mais plus
         d'adaptation à la largeur de fenêtre ;
      3. **mémoriser les largeurs** dans `state.json` et neutraliser le `*` des colonnes touchées
         — le confort attendu, dans la continuité de la persistance déjà en place.
### Références externes (pour la suite)
- Format V1/V2 : `https://git.codence.de/pub/tonex-library-sync` (open-source, lit le format V1).
- Les outils de référence `bcho/` (Bcho-Suite-Pro, seul outil connu lisant la V2) et
  `ToneXdbExplorer/` ont été **supprimés du workspace** — à retélécharger si besoin.

### Limitations connues (volontaires pour la v0.1)
- **V2 (`Library2.db`)** : métadonnées uniquement, pas de réglages numériques → signalé dans l'IU
  via `SettingsNote`.
- Aucune écriture dans les bases TONEX (contrainte non négociable), y compris pour les favoris.
- Le conseil IA s'appuie sur le classement local (top 3) : c'est voulu, pour rester dans le
  budget de tokens et ne jamais faire inventer de preset au modèle.

---

## 🔧 Comment builder / tester

```powershell
$dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
& $dotnet build TonexAdvisor.sln -v minimal
& $dotnet test tests\TonexAdvisor.Core.Tests\TonexAdvisor.Core.Tests.csproj -v minimal
& ".\src\TonexAdvisor.App\bin\Debug\net9.0\TonexAdvisor.App.exe"
```

> ⚠️ Ne jamais ouvrir `D:\OneDriveBruno\OneDrive\Documents\IK Multimedia\TONEX` :
> seules `db\Library.db` et `db\Library2.db` du workspace sont utilisées.
