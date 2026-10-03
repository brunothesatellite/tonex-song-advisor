# TODO — Tonex Song Advisor

Version **0.1 testable** : la lecture des bibliothèques TONEX + l'IU sont en place.
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
- **Tests** : 109 tests verts (données, readers V1/V2, tokeniseur, ranges, panneau de détail, filtres,
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

---

## 🔜 Reste à faire

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
### Phase 5 — Fiabilité et finitions
- [x] Test d'intégrité **automatisé** : hash des 2 bases avant/après une session complète
      (lecture, conseil, clics dans l'IU) + aucun journal SQLite ni pages en attente.
- [ ] Persistance des favoris et des filtres (fichier JSON local) — actuellement éphémères.
- [x] Tri par colonne vérifié : clic sur « Nom » → ordre croissant, second clic → décroissant
      (vérifié par automatisation UI, colonne lue avant/après).
- [ ] Kolonne « Réglages » : valeur `✔`/`—` — vérifier que le `✔` (U+2714) est rendu par la police,
      sinon remplacer par un `Path` vectoriel comme pour l'étoile.
- [x] Sélection de ligne aux couleurs du thème : fond orange translucide + liseré accent.
- [ ] Mode sombre de la barre de tâches / icône d'application.
- [x] Publication autonome vérifiée (`dotnet publish -c Release -r win-x64 --self-contained`,
      230 fichiers / 206 Mo, commande documentée dans le README). Reste : un vrai installeur.
- [x] CI GitHub Actions (`.github/workflows/ci.yml`) : build + tests à chaque modification.
      Les tests dépendants des bases (jamais versionnées) sont ignorés automatiquement
      (`[LibraryFact]`), la CI vérifie le reste.

### Phase 6 - Avis croises multi-IA (evolution)
Objectif : ne plus s'en tenir a un seul modele. Croiser l'avis d'**OpenCode** (deja branche), de
**Gemini (gratuit)** et de **Copilot (gratuit)**, puis faire arbitrer les trois avis pour ne
garder que les **3 meilleures propositions**. Gemini et Copilot sont explicitement invites a
**contester** la recommandation de reference (preset, bloc capture, reglages), pas a la
confirmer : c'est le desaccord qui fait la valeur de ce mode.

- [ ] Interface commune `AiProvider` (`AskStreamAsync` + nom + modele), reimplementee par
      `OpenCodeClient` existant. Un fournisseur = un connecteur, jamais de code specialise dans
      l'IU.
- [ ] Connecteur **Gemini gratuit** : endpoint OpenAI-compatible
      `https://generativelanguage.googleapis.com/v1beta/openai/chat/completions`, cle AI Studio
      (quota gratuit), cle stockee comme celle d'OpenCode (`%APPDATA%\TonexAdvisor\config.json`,
      hors repo).
- [ ] Connecteur **Copilot gratuit** : **point a valider avant tout developpement** - Microsoft
      Copilot n'expose pas d'API publique gratuite a ce jour ; a confirmer (API GitHub Copilot,
      Copilot Studio ?). Si aucun acces n'existe, documenter le refus et fonctionner a deux voix
      sans casser la fonctionnalite.
- [ ] Consultation **en parallele** (3 requetes, meme contexte `AdvicePrompt` et meme regle
      « stomp + ampli inseparables, seul le baffle se change »), avec delai maximal (ex. 20 s)
      et collecte des erreurs par fournisseur.
- [ ] Prompt de **confrontation** : demander a Gemini/Copilot de relever ce qui cloche dans la
      proposition de reference et de proposer une alternative issue de la meme liste locale,
      jamais un preset hors liste.
- [ ] **Arbitrage** : tour de synthese qui classe les 3 meilleures propositions, chacune avec
      l'avis des 3 IA, les points de desaccord et un niveau de consensus (3/3, 2/3).
- [ ] IU : carte « Avis croises » sur l'onglet Conseils - colonnes OpenCode / Gemini / Copilot,
      badge de desaccord, bouton « Synthese » et resultat de l'arbitrage.
- [ ] Repli : un fournisseur en echec ou en timeout est signale et le verdict est rendu a 2
      voix ; si un seul repond, on retombe sur le mode mono-IA actuel.
- [ ] Tests : connecteurs factices (succes, erreur, timeout), arbitrage deterministe a partir de
      trois avis ecrits, et absence de fuite de cles.
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
