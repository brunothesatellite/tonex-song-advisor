# Tonex Song Advisor

> 🇫🇷 **Français : [README.md](README.md)**

Desktop application (.NET 9 + Avalonia 12) that **reads** the IK Multimedia TONEX libraries
(V1 `Library.db` and V2 `Library2.db` formats), displays them in a grid, and advises the best
preset / the best **amp + stomp + cab** combination for a given song or artist.

> 🔒 **Strictly read-only access.** The TONEX databases are never modified:
> `Mode=ReadOnly` + `Pooling=False` + `PRAGMA query_only=ON` + SQL verb filtering
> (`SELECT`/`WITH`/`EXPLAIN` only), with SHA-256 fingerprints captured on open and re-verified
> on close.

## Installation

Two formats are published with every [release](https://github.com/brunothesatellite/tonex-song-advisor/releases) :

| File | Usage |
|---|---|
| `TonexSongAdvisor-x.y-setup.exe` | **Install** : per user, no admin rights — Start menu and desktop shortcuts, an entry in Windows "Add or remove programs" with uninstall. |
| `TonexSongAdvisor-x.y-portable.zip` | **Portable** : unzip and run `TonexAdvisor.App.exe`, nothing is installed. |

Windows 10/11 x64, no .NET installation required (self-contained application).

> **Unsigned executables** : the executables are not signed — Windows SmartScreen shows
> "App unrecognized": click "More info" then "Run anyway".

The installer (`tools/Setup`) is **project code** : no third-party utility is used to build it.
Reminder of the project's license rule — WiX is under MS-RL (reciprocal copyleft), NSIS and
Inno Setup under off-list licenses : we therefore build our own installer, MIT like the rest.

To rebuild both artifacts : `powershell -ExecutionPolicy Bypass -File tools\build-release.ps1 -Version x.y`.

## User manual

The screenshots come from a sample library : your preset names will differ.

### 1. The Library screen (Presets tab)

<img src="docs/images/01-presets.png" alt="Presets tab: search, filters, grid and detail panel" width="780">

- **Search** : a single field covers everything (name, artist, song, amp, folder, author).
  Multiple words = all must be present.
- **Filters** : category, genre, folder, and the **Favorites** button that keeps the presets
  starred in TONEX.
- **Reset** clears search and filters.
- Columns **sort** (one click : ascending, two : descending) and can be **resized by hand** :
  the width you choose is kept, even across restarts.
- The captured chain reads left to right : **Stomp → Amp → Cab**. The stomp and the amp come from
  the same capture and cannot be separated ; the cab is the only interchangeable part — even for
  a "full rig", whose stomp stays displayed even without a named amp.
- The **right-hand panel** details the selected row : metadata, linked tone model, read-only
  knobs, collapsible hardware sections.
- The **Settings** column shows (orange tick or dash) whether the preset carries usable numeric
  settings : the case in generation 1, never in generation 2.
- Your filters and the active tab are **restored on the next launch**
  (`%APPDATA%\TonexAdvisor\state.json`).

### 2. The Tone models tab

<img src="docs/images/02-tone-models.png" alt="Tone models tab: amp captures, cabs, mics" width="780">

The captures (stomp, amp, cab, mics) and how many presets use them. The selection feeds the same
detail panel.

### 3. The Advice tab : the local ranking

<img src="docs/images/03-conseils-local.png" alt="Advice: best presets and captured block with cab" width="780">

1. Describe the song : **artist**, **song**, **style/vibe** ("metal", "blues saturé",
   "clean funk") — one field is enough.
2. Click **Recommend** : the library is ranked in a few milliseconds, offline, and the screen
   shows :
   - the **3 best presets**, with score and reasons ("Category HI-GAIN : saturation suited to
     \"metal\"", "\"Metal\" genre", "Library favorite"…) ;
   - the **best captured block**, `stomp → amp` : both come from the **same capture** and are
     inseparable ;
   - the recommended **cab**, always flagged as **replaceable by any other cab** — the only
     interchangeable part in TONEX.
3. **Open** selects the matching row in the grid, filters reset.

### 4. The AI advice

<img src="docs/images/04-conseils-ia.png" alt="AI advice streaming on top of the local ranking" width="780">

- **Ask the AI** sends the request + your library catalogue : each amp with the stomps captured
  with it, and the list of cabs — **names only**, most-used first (200 amps + 120 cabs,
  ≈ 3,400 tokens : the size free quotas accept). The AI therefore chooses knowing the ground,
  even on names local search cannot guess, while respecting the "stomp + amp inseparable" rule —
  the cab remaining free.
- The answer has **two parts** : what your library allows (`BLOCK`, `CAB`, `SETTINGS`,
  `ALTERNATIVE`), then a **FREE ADVICE** — what the AI itself would use with no constraint : the
  real gear for the track or the style, and the typical settings that go with it. This is the
  useful part when your library has nothing really close.
- The answer streams in **live** ; the chain of thought is displayed during generation then
  disappears once the answer arrives.
- Missing key, network down, quota exceeded : an explicit message and **the local ranking stays
  displayed** — the AI is a bonus, never the only source of advice.
- **Cancel** interrupts generation.

### 5. Databases & settings : the API key

<img src="docs/images/05-reglages.png" alt="Databases and settings screen: API key, model, read-only guarantee" width="780">

- The OpenCode key is entered here (masked on screen) and stored in
  `%APPDATA%\TonexAdvisor\config.json`, **outside the repo**.
- Choice of the **free model** (LongCat 2.5 Preview Free by default, Space Bunny Free) and a
  **Test connection** button.
- The screen also shows the detected format (V1 or V2) and restates the read-only guarantee.

### What the application never does

- it **never writes** to the TONEX databases : SHA-256 fingerprints verified before/after by the
  tests, `Mode=ReadOnly` connection, `PRAGMA query_only` and SQL verb filtering ;
- it **never opens** the `Documents\IK Multimedia\TONEX` folder : only the project's copies
  `db\Library.db` and `db\Library2.db` are read ;
- it versions **no key** and no database (see `.gitignore`).

## Building and testing

```powershell
$dotnet = "$env:USERPROFILE\.dotnet\dotnet.exe"
& $dotnet build TonexAdvisor.sln -v minimal
& $dotnet test tests\TonexAdvisor.Core.Tests\TonexAdvisor.Core.Tests.csproj -v minimal
& ".\src\TonexAdvisor.App\bin\Debug\net9.0\TonexAdvisor.App.exe"
```

Prerequisites : .NET 9 SDK.

### Publishing a self-contained build

```powershell
& $dotnet publish src\TonexAdvisor.App\TonexAdvisor.App.csproj -c Release -r win-x64 `
    --self-contained -o .\publish
```

The `publish\` folder contains the executable (icon included) and everything needed to run
without .NET installed (~200 MB). It is outside the repo.

## OpenCode API key (AI advice)

The API key is **never versioned** : it is stored in a file outside the repo,

```
%APPDATA%\TonexAdvisor\config.json
```

created automatically on the first **Save** from *Settings → AI advice*. You can also create it
yourself :

```json
{
  "ApiKey": "oc_sk_…",
  "Model": "longcat-2.5-preview-free",
  "Endpoint": "https://opencode.ai/zen/go/v1"
}
```

Where to get one : <https://opencode.ai/settings/api>. The `.gitignore` also forbids
`config.json`, `*.config.json`, `*.secrets.json` and `.env*` inside the repo, for safety.

**Only the free models ("Free", unlimited) are offered** in the dropdown :

| Model | Offer | Available with an API key? |
|---|---|---|
| `longcat-2.5-preview-free` (default) | OpenCode Go | ✅ yes |
| `space-bunny-free` | OpenCode Go | ✅ yes |
| `mimo-v2.6-flash-free`, `ling-3.1-flash-free`, `nemotron-3.5-lightning-free`, `fledge-alpha-free`, `muse-spark-1.3-contributor-free`… | Personal (Zen) | ❌ `403 FreeTierError` |

The **Personal** free tier only accepts OAuth sessions from the OpenCode application
(`OpenCode's free tier can only be used from within OpenCode`) : these models are therefore
ruled out for a third-party application using only an `oc_sk_` key. The **Test connection**
button checks the key and the chosen model with a real request.

### Choosing your library ("Databases & settings" screen)

Two ways, your choice :

1. **Manual path** : type or "Browse" to a `Library.db` / `Library2.db` file, then "Open".
2. **TONEX folder** : tick "Use a database from the TONEX folder" and pick from the list — the
   folder is found through the Windows "Documents" *Known Folder*, so it is correct even when
   Documents is redirected to OneDrive. Each row shows the name, the generation (V1/V2), the size
   and the date.

Ticking the box greys out the path ; unticking returns to option 1 and reloads the path database.
The choice is **remembered** across restarts. In both cases, if the database is generation 2 and
a V1 sits next to it, its settings are joined automatically.

## API keys (cross-checked opinions)

The advice can cross several opinions : **OpenCode** (the reference) and, **if you fill in their
keys**, **Gemini**, **Mistral** and **Groq**. Each voice receives the same request and the same
catalogue of blocks and cabs, then answers independently — often disagreeing — then an arbiter
weighs the opinions and ranks the **3 best proposals** with their level of consensus.

| Voice | Where to get an API key | Default free model |
|---|---|---|
| **OpenCode Go** (reference) | <https://opencode.ai/> (*Go* plan) | `longcat-2.5-preview-free` |
| **Gemini** (Google) | <https://aistudio.google.com/apikey> | `gemini-flash-latest` |
| **Mistral** | <https://console.mistral.ai/api-keys> | `mistral-small-latest` |
| **Groq** | <https://console.groq.com/keys> | `openai/gpt-oss-120b` |

- Keys are entered in **Databases & settings**, masked, and stored in
  `%APPDATA%\TonexAdvisor\config.json` — **never in the repo**.
- You don't have to fill them all in : **a missing key = a silent voice**, and the advice stays
  valid with the ones you have. One key = a direct opinion, two or more = conflicting opinions
  then arbitration.
- Each voice has its own timeout (180 s) : a slow or failing provider is simply flagged in the
  card, without failing the others.
- The models above are free and can be overridden from `config.json`
  (`Providers.<id>.Model`) if pricing changes. All these APIs are OpenAI-compatible : a single
  connector drives them.

## Test data

The TONEX databases are **not versioned** (personal library, 89 MB). To run the 195 tests, copy
your own files into `db/` :

```
db\Library.db     ← V1 format (complete numeric settings)
db\Library2.db    ← V2 format (metadata)
```

## Structure

| Folder | Role |
|---|---|
| `src\TonexAdvisor.Core` | database reading (read-only), DTOs, index, tokenizer, ranges |
| `src\TonexAdvisor.App` | Avalonia UI, `Themes\DarkRock.axaml` theme, view models |
| `tests\TonexAdvisor.Core.Tests` | 195 tests |
| `TODO.md` | status and remaining work |

## Status

**v1.2.1** — details in [TODO.md](TODO.md) :

- **strictly read-only** reading of the TONEX **V1 and V2** libraries, browser (presets and tone
  models), sorting, remembered filters, detail panel ;
- **Local advice** : the best preset and the best captured block (stomp + amp) with its cab,
  every point explained — offline, in milliseconds ;
- **Cross-checked opinions** : OpenCode and the voices you fill in (Gemini, Mistral, Groq) choose
  from your catalogue of blocks and cabs, then an **arbitrated verdict** by OpenCode between its
  proposal and the voices', with its free advice (the real gear for the track) ;
- **Generation 2 settings** : the values are not in Library2.db (encrypted in IK's .txp files) —
  when a V1 library sits next to it, they are joined automatically (2,313 presets on the sample
  library), with their origin stated.
- **Installer** and **portable build**, GitHub Actions CI, 195 tests, read-only guarantee
  verified by SHA-256 fingerprint.

Remaining : **Phase 8 — internationalization (French / English)** ; the detailed plan is in
[INTERNATIONALISATION.md](INTERNATIONALISATION.md), progress in [TODO.md](TODO.md).
