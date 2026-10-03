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
- **Tests** : 70 tests verts (données, readers V1/V2, tokeniseur, ranges, panneau de détail, filtres).
- **Barre de titre Windows** en mode sombre (`DwmSetWindowAttribute`).

### Corrections récentes (dernier cycle)
- Le glyphe `★` (U+2605) n'existe pas dans Inter → **jamais rendu**. Remplacé par une étoile
  **vectorielle** (`Path`, style `Path.favStar` / `.favOff`) dans les cellules, les favoris,
  les cartes de détail et le bouton « Favoris ».
- Colonne « Réglages » et colonne « Fav » élargies (44 → 64/88 px) : sous ~44 px l'en-tête de
  colonne était rogné à zéro pixel et disparaissait.

---

## 🔜 Reste à faire

### Phase 3 — Recommandation locale (priorité haute)
- [ ] `Scorer` dans `TonexAdvisor.Core` : noter presets/tone models pour une chanson/artist donnés.
- [ ] Signaux à combiner : genres, tags, artiste/chanson déjà associés, catégorie, dossiers,
      plage de réglages (gain/volume/master), nom du preset.
- [ ] Classement « meilleur preset » + « meilleure combinaison ampli + stomp + cab ».
- [ ] Explication lisible de chaque conseil (« pourquoi ce preset »).
- [ ] Onglet/section « Conseils » dans l'IU avec les 3 meilleures propositions cliquables.

### Phase 4 — Conseil IA via OpenCode Go (priorité haute)
- [ ] Client HTTP : `https://opencode.ai/zen/go/v1/chat/completions` (OpenAI-compatible,
      `Authorization: Bearer`, `x-opencode-session`, `User-Agent: TonexAdvisor/1.0`).
- [ ] Saisie de la clé API + stockage local (fichier de config, jamais en clair dans le repo).
- [ ] Streaming SSE + affichage progressif du conseil.
- [ ] Contexte envoyé au modèle : chanson/artist + résumé des N presets les mieux notés (pas toute
      la bibliothèque, pour rester dans les limites de tokens).
- [ ] Gestion d'erreur : hors-ligne, clé invalide, quota dépassé → message + repli sur le
      classement local (Phase 3).

### Phase 5 — Fiabilité et finitions
- [ ] Test d'intégrité avant/après **automatisé** dans les tests (hash des 2 bases avant run,
      après run, assertion égale).
- [ ] Persistance des favoris et des filtres (fichier JSON local) — actuellement éphémères.
- [ ] Tri par colonne vérifié visuellement (le tri est activé, pas encore contrôlé pixel par pixel).
- [ ] Kolonne « Réglages » : valeur `✔`/`—` — vérifier que le `✔` (U+2714) est rendu par la police,
      sinon remplacer par un `Path` vectoriel comme pour l'étoile.
- [ ] Sélection de ligne : fond actuel généré par le thème Fluent d'Avalonia, à revoir pour
      l'orange du thème (priorité basse).
- [ ] Mode sombre de la barre de tâches / icône d'application.
- [ ] Installeur / publication (`dotnet publish -r win-x64 --self-contained`).
- [ ] CI : build + 70 tests à chaque modification.

### Références externes (pour la suite)
- Format V1/V2 : `https://git.codence.de/pub/tonex-library-sync` (open-source, lit le format V1).
- Les outils de référence `bcho/` (Bcho-Suite-Pro, seul outil connu lisant la V2) et
  `ToneXdbExplorer/` ont été **supprimés du workspace** — à retélécharger si besoin.

### Limitations connues (volontaires pour la v0.1)
- **V2 (`Library2.db`)** : métadonnées uniquement, pas de réglages numériques → signalé dans l'IU
  via `SettingsNote`.
- Aucune écriture dans les bases TONEX (contrainte non négociable), y compris pour les favoris.
- Le conseil IA n'est pas encore branché (Phase 4).

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
