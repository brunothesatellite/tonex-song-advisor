# Tonex Song Advisor

Application de bureau (.NET 9 + Avalonia 12) qui **lit** les bibliothèques IK Multimedia TONEX
(format V1 `Library.db` et V2 `Library2.db`), les affiche en tableau et conseille le meilleur
preset / la meilleure combinaison **ampli + stomp + cab** pour une chanson ou un artiste donné.

> 🔒 **Accès strictement en lecture seule.** Les bases TONEX ne sont jamais modifiées :
> `Mode=ReadOnly` + `Pooling=False` + `PRAGMA query_only=ON` + filtrage des verbes SQL
> (`SELECT`/`WITH`/`EXPLAIN` uniquement), et empreintes SHA-256 capturées à l'ouverture puis
> revérifiées à la fermeture.

## Compiler et tester

```powershell
$dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
& $dotnet build TonexAdvisor.sln -v minimal
& $dotnet test tests\TonexAdvisor.Core.Tests\TonexAdvisor.Core.Tests.csproj -v minimal
& ".\src\TonexAdvisor.App\bin\Debug\net9.0\TonexAdvisor.App.exe"
```

Prérequis : SDK .NET 9.

## Clé API OpenCode (conseil IA)

La clé API n'est **jamais versionnée** : elle est stockée dans un fichier hors dépôt,

```
%APPDATA%\TonexAdvisor\config.json
```

créé automatiquement au premier **Enregistrer** depuis *Réglages → Conseil IA*. Tu peux aussi le
créer toi-même :

```json
{
  "ApiKey": "oc_sk_…",
  "Model": "longcat-2.5-preview-free",
  "Endpoint": "https://opencode.ai/zen/go/v1"
}
```

Où l'obtenir : <https://opencode.ai/settings/api>. Le `.gitignore` interdit également
`config.json`, `*.config.json`, `*.secrets.json` et `.env*` à l'intérieur du dépôt, par sécurité.

**Seuls les modèles gratuits (« Free », illimités) sont proposés** dans la liste déroulante :

| Modèle | Offre | Disponible avec une clé API ? |
|---|---|---|
| `longcat-2.5-preview-free` (défaut) | OpenCode Go | ✅ oui |
| `space-bunny-free` | OpenCode Go | ✅ oui |
| `mimo-v2.6-flash-free`, `ling-3.1-flash-free`, `nemotron-3.5-lightning-free`, `fledge-alpha-free`, `muse-spark-1.3-contributor-free`… | Personnel (Zen) | ❌ `403 FreeTierError` |

Le free tier **Personnel** n'accepte que les sessions OAuth de l'application OpenCode
(`OpenCode's free tier can only be used from within OpenCode`) : ces modèles sont donc écartés
d'une application tierce qui n'utilise qu'une clé `oc_sk_`. Le bouton **Tester la connexion**
vérifie la clé et le modèle choisi par une vraie requête.

## Données de test

Les bases TONEX ne sont **pas versionnées** (bibliothèque personnelle, 89 Mo). Pour exécuter les
95 tests, copie tes propres fichiers dans `db/` :

```
db\Library.db     ← format V1 (réglages numériques complets)
db\Library2.db    ← format V2 (métadonnées)
```

## Structure

| Dossier | Rôle |
|---|---|
| `src\TonexAdvisor.Core` | lecture des bases (read-only), DTOs, index, tokeniseur, ranges |
| `src\TonexAdvisor.App` | UI Avalonia, thème `Themes\DarkRock.axaml`, ViewModels |
| `tests\TonexAdvisor.Core.Tests` | 95 tests |
| `TODO.md` | état d'avancement et travail restant |

## Avancement

Voir [TODO.md](TODO.md) — v0.1 : lecture + navigateur + détail des presets, réglages IA
(clé API + modèles gratuits + test de connexion). Voir [TODO.md](TODO.md) - v0.1 : lecture + navigateur + détail des presets, réglages IA
(clé API + modèles gratuits + test de connexion), recommandation locale avec l'onglet
« Conseils » (meilleurs presets + bloc capturé et baffle). Reste : conseil IA branché à cet
écran (Phase 4 : streaming SSE, contexte envoyé au modèle, repli sur le classement local).
