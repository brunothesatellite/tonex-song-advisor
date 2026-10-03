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

## Données de test

Les bases TONEX ne sont **pas versionnées** (bibliothèque personnelle, 89 Mo). Pour exécuter les
70 tests, copie tes propres fichiers dans `db/` :

```
db\Library.db     ← format V1 (réglages numériques complets)
db\Library2.db    ← format V2 (métadonnées)
```

## Structure

| Dossier | Rôle |
|---|---|
| `src\TonexAdvisor.Core` | lecture des bases (read-only), DTOs, index, tokeniseur, ranges |
| `src\TonexAdvisor.App` | UI Avalonia, thème `Themes\DarkRock.axaml`, ViewModels |
| `tests\TonexAdvisor.Core.Tests` | 70 tests |
| `TODO.md` | état d'avancement et travail restant |

## Avancement

Voir [TODO.md](TODO.md) — v0.1 : lecture + navigateur + détail des presets.
Prochaines étapes : recommandation locale (Phase 3), conseil IA via l'API OpenCode Go (Phase 4).
