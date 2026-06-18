# Setup Guide — DungeonExporer

> Install, run, and playtest the current Level1 slice.  
> **Canonical requirements** for hardware, software, gameplay features, and course submission.  
> Last updated: 2026-06-18

## 1. Game requirements

### 1.1 To play (player / assessor)

| Requirement | Details |
|---|---|
| **OS** | Windows 10 / 11 (primary). Linux & macOS work for Ollama; exported builds target Windows. |
| **CPU** | Modern x86-64 (Intel 8th gen / Ryzen 2000 or newer). |
| **RAM** | **16 GB** minimum; **32 GB** recommended when running Unity + Ollama together. |
| **GPU** | Integrated graphics OK for `qwen3:4b` in CPU mode; dedicated GPU with **≥ 8 GB VRAM** recommended for faster inference. |
| **Disk** | ~10 GB for game + Ollama + one model; ~20 GB on SSD if pulling multiple models. |
| **Ollama** | [Ollama](https://ollama.com/download) installed and running on `http://localhost:11434`. |
| **LLM model** | At least one pulled model matching gameplay settings — default **`qwen3:4b`** (`ollama pull qwen3:4b`). Optional **`gemma3:4b`** for **Fast AI responses** (Options). |
| **Internet** | Not required at runtime after models are pulled. Required once to install Ollama and download models. |
| **Python / Jinja2** | **Not required** to play. Cap prompts render in C# from `Assets/Prompts/cap_personality.j2` (`CharacterPersonalityTemplateManager`). |

**Without Ollama:** the game still runs. Quest UI, combat, maze, and save/load work. Cap dialogue falls back to authored text when **AI-driven dialogue** is off or Ollama is unreachable (`OllamaSetupPanelController` → **Continue**).

### 1.2 To develop (Unity contributor)

| Requirement | Details |
|---|---|
| **Unity Editor** | **6000.3.8f1** (`ProjectSettings/ProjectVersion.txt`) via [Unity Hub](https://unity.com/download). |
| **Git** | Clone and version the repo. |
| **Ollama** | Same as player requirements — needed to test LLM features in Play mode. |
| **IDE** | Visual Studio 2022, Rider, or VS Code for C#. |
| **Python 3 + Pillow** | Optional — regenerate dungeon textures (`Tools/generate_dungeon_textures.py`). |
| **Python 3 + Jinja2** | Optional — offline testing of legacy `prompts/cap_personality.jinja2` only; not used at runtime. |

### 1.3 Functional requirements (Level1 slice)

What the current build must deliver for playtest and submission:

| Area | Requirement | Implementation |
|---|---|---|
| **Exploration** | First-person movement in a hybrid ASCII maze (`#` walls, `.` corridors, **S** safe hubs, **E** encounter pits). | `DungeonLevelBuilder`, `Level1_Maze.txt` |
| **Quests** | Authored main quests + optional AI side quests; progress tracked, completion indicated in HUD, saveable. | **Cap**, `QuestManager`, `AiQuestPlanner`, `QuestWorldEvents` |
| **Combat** | Melee combat with foes that chase and attack; player attack with feedback. | `PlayerCombat`, `EnemyMeleeAI`, `CombatHitVfx` |
| **LLM dialogue** | Cap voice lines on open + **Another line** (no player-typed chat). | `OllamaHandler` (`/api/generate`), `DialoguePanelController` |
| **LLM flavor** | Environment narration tied to zone type. | `DungeonFlavorNarrator` on **S** / **E** tiles |
| **LLM planning** | Optional JSON placement hints validated in C# (not authoritative geometry). | `DungeonTrapPlanner`, `DungeonContentPlanner` |
| **Local inference** | All runtime LLM calls to localhost Ollama only — no cloud API. | `OllamaHandler` → `localhost:11434` |
| **Player opt-out** | Disable AI dialogue without blocking core gameplay. | Options → **AI-driven dialogue (Ollama)** |
| **Onboarding** | Controls and tips available before play. | **Main Menu → How to Play** |
| **Persistence** | Session save / load. | **F5** / **F9**, `GameSaveService` |
| **Transparency** | Setup panel when Ollama or model is missing; link to this guide. | `OllamaFirstRunHealthCheck`, `OllamaSetupPanelController` |

Authoritative game facts (quest **objective event ids**, maze layout, combat rules) stay in **C#** — the LLM adds flavor, dialogue, and side-quest *wording* only (objectives must match `QuestWorldEvents`).

### 1.4 Course submission requirements (academic brief)

Maps **Structure & Deliverables** from the project brief. Full checklist: [`deliverables-checklist.md`](./deliverables-checklist.md).

| Deliverable | In repo? | Where |
|---|---|---|
| Documentation pack (high concept, Ollama plan, setup, ethics, prompt archive, integration report) | Yes | `docs/` — see README table |
| Prototype with ≥1 LLM feature | Yes | Play **Level1** in Editor |
| Refinements / change log | Yes | [`refinements-changes.md`](./refinements-changes.md) |
| Playtest feedback + critical engagement | Yes | [`feedback.summary.md`](./feedback.summary.md), [`critical.feedback.md`](./critical.feedback.md) |
| AI usage disclosure (annexure) | Yes | [`ai-usage-annexure.md`](./ai-usage-annexure.md) |
| Technical video (3–6 min, local LLM integration) | **You record** | [`video-deliverables.md`](./video-deliverables.md) §1 |
| Showcase video (3–6 min, gameplay + design intent) | **You record** | [`video-deliverables.md`](./video-deliverables.md) §2 |
| Final stable build (.exe) | **You export** | [`build-notes.md`](./build-notes.md) |
| Credits (your name) | **You fill in** | [`README.md`](../README.md) Credits |

---

## 2. System requirements (summary)

*Duplicate of §1.1 for quick reference.*

### Minimum (developer / playtest)

- **OS**: Windows 10 / 11 (Linux & macOS work for Ollama; Unity dev tested on Windows).
- **CPU**: Modern x86-64 (Intel 8th gen / Ryzen 2000 or newer).
- **RAM**: 16 GB.
- **GPU**: Integrated graphics OK for `qwen3:4b` in CPU mode.
- **Disk**: ~10 GB (Unity + Ollama + one model + Meshy imports).

### Recommended

- **CPU**: 6+ cores.
- **RAM**: 32 GB.
- **GPU**: Dedicated GPU, ≥ 8 GB VRAM.
- **Disk**: SSD, ~20 GB if pulling multiple models.

## 3. Tooling

| Tool | Version | Purpose |
|---|---|---|
| [Unity Hub](https://unity.com/download) | latest | Editor installs |
| Unity Editor | **6000.3.8f1** (`ProjectSettings/ProjectVersion.txt`) | Engine |
| [Git](https://git-scm.com/) | latest | Source control |
| [Ollama](https://ollama.com/download) | latest | Local LLM (runtime) |
| Python 3 + Pillow | optional | Regenerate dungeon textures (`Tools/generate_dungeon_textures.py`) |
| Python 3 + Jinja2 | optional | Legacy offline test of `prompts/cap_personality.jinja2` only — **not** required at runtime |
| IDE | Visual Studio 2022 / Rider / VS Code | C# |

## 4. Clone the repo

```powershell
git clone https://github.com/<your-org>/DungeonExporer.git
cd DungeonExporer
```

## 5. Install Ollama

### Windows

1. Download from <https://ollama.com/download> and install (background service on `http://localhost:11434`).
2. Verify:

```powershell
curl http://localhost:11434/api/tags
```

### Linux / macOS

```bash
curl -fsSL https://ollama.com/install.sh | sh
```

## 6. Pull the required model(s)

Default:

```powershell
ollama pull qwen3:4b
```

Optional quality tier:

```powershell
ollama pull llama3
```

Verify:

```powershell
ollama list
ollama run qwen3:4b "Say hello in five words."
```

The scene **`OllamaHandler`** component's **Default Model** must match a name from `ollama list` exactly (e.g. `qwen3:4b`). Gameplay reads `GameSettings.LlmModel` first (PlayerPrefs default `qwen3:4b`).

**Fast AI responses** (Options → toggle): switches to `gemma3:4b`, shorter Cap prompts, lower token caps, and less chat memory. Pull it if you use fast mode:

```powershell
ollama pull gemma3:4b
```

### Cap prompts (runtime — no Python)

Cap personality prompts are rendered in C# by `CharacterPersonalityTemplateManager` from:

- `Assets/Prompts/cap_personality.j2`
- `Assets/Prompts/cap_context.json` (test UI defaults)

Legacy Jinja2 files under `prompts/` remain for documentation and optional offline comparison only.

## 7. Open the project in Unity

1. Unity Hub → **Add** → select the repo folder.
2. Install editor **6000.3.8f1** if prompted.
3. First import may take several minutes (Meshy FBX under `Assets/Models/`, environment textures).

### First-time mesh / material checks

- **Cap / foes**: `GameplaySystems` → `LevelGameplayBootstrap` should reference FBX under `Assets/Models/` (`_npcModel`, `_enemyModel`). If empty, select the component in the editor — paths auto-assign via `GameplayModelPaths`.
- **Dungeon**: `Dungeon` object → `_wallMaterial`, `_floorMaterial` on `DungeonLevelBuilder`.
- **Spikes**: `GameplaySystems` → `_spikeTrapMaterial`.
- **Adventurer** (optional): extract materials from `Assets/Art/Characters/Adventurer/Adventurer.fbx` — see [`art-direction.md`](./art-direction.md).

## 8. Run the game

**Recommended (full flow):**

1. Open `Assets/Scenes/MainMenu.unity`.
2. Ensure Ollama is running.
3. Press **Play** → optional **How to Play** → start Level1.

**Direct Level1 test:** open `Assets/Scenes/Level1.unity` and press **Play** (skips main-menu warm-up).

### Boot flow

- **Main Menu** (`OllamaMenuWarmup`): when LLM is enabled, issues a short warm-up completion so Level1 hits a hot model. **How to Play** lists controls and tips (scrollable panel).
- **`OllamaFirstRunHealthCheck`**: pings Ollama and checks the model tag; on failure, **`OllamaSetupPanelController`** offers a link to this guide and **Continue** (play without LLM voice).
- **`DungeonLevelBuilder`**: builds maze from `Assets/Data/Dungeon/Level1_Maze.txt`, places player at **P**.
- **`LevelGameplayBootstrap`**: spawns **Cap**; Ollama JSON plans for loot, foes, signs, and traps run **sequentially** after a short spawn delay and **pause while Cap dialogue is open**; procedural fill on failure; attaches **`EnemyMeleeAI`** to foes.
- **`GameSaveService`**: auto-loads `dungeon_session_save.json` if present.

### Controls (keyboard & mouse)

> The same bindings are listed in-game under **Main Menu → How to Play**.

| Action | Binding |
|--------|---------|
| Move | WASD |
| Look | Mouse |
| Jump | Space |
| Sprint | Left Shift |
| Crouch | C |
| Interact | **E** (hold where required) |
| Attack | **Left click** (also **Enter**) |
| Inventory | **I** |
| Pause | Escape |
| Save session | **F5** |
| Load session | **F9** |

Gamepad: Attack = west face button; Interact = north; see `InputSystem_Actions` for full map.

### Dungeon look

The **Dungeon** object builds **5.5m** stone walls, a **ceiling** on every walkable cell, and **torch lighting** (room-centre + grid-fill torches, coverage pass, warm point lights + boosted sun/ambient). Hazards use high-contrast spike albedos and a pulsing emissive marker (`HazardTrapVisual`). Tune on **Dungeon** → `DungeonLevelBuilder`: `_wallHeight`, `_torchIntensity`, `_maxTorches`, `_directionalIntensity`.

### Playtest checklist

0. **Main Menu → How to Play** — skim controls before entering Level1.
1. Find **Cap** (safe **S** room near spawn).
2. **E** → accept **Cap's corridor drill**; read Cap's auto voice line (Ollama prefetch).
3. Optional: **Another line** for a fresh Cap voice roll.
4. Go to crimson **E** floors; foes **chase and melee** — **left-click** to attack until a **DungeonFoe** dies (~2 hits). Confirm **Quest complete: …** toast and objective-line message.
5. Return to Cap; accept **Echoes in the dark** if offered; step onto an **E** encounter tile for the second quest.
6. After main quests, talk to Cap again for optional **AI side quests** (pebble pickup, safe-room visit, etc.) if offered.
7. Walk through bubble pickups; jump narrow spike strips; read corridor **signs**.
8. **F5** / **F9** save and load (includes dynamic AI quest text); toggle **AI-driven dialogue (Ollama)** in Options to verify canned Cap line when off. Enable **Fast AI responses** if Cap voice feels slow (requires `gemma3:4b`).

### Ollama tester UI

Level1 still includes an on-screen **Ollama** test panel (`OllamaHandler`) for raw prompt/stream debugging alongside gameplay dialogue.

## 9. Regenerate environment textures (optional)

```powershell
pip install Pillow
python Tools/generate_dungeon_textures.py
```

Reimports: `DungeonBrick_Albedo.png`, `DungeonFloor_Albedo.png`, `SpikeTrap_Albedo.png`.

## 10. Project structure (current)

```
DungeonExporer/
├── AGENTS.md
├── README.md
├── docs/
├── prompts/                       # cap_personality.jinja2, render_cap_prompt.py
├── Tools/generate_dungeon_textures.py
├── Assets/
│   ├── Scenes/MainMenu.unity, Level1.unity
│   ├── Data/Dungeon/Level1_Maze.txt
│   ├── Models/                    # Meshy: Cap + Grumblemite FBX
│   ├── Art/Characters/Adventurer/
│   ├── Art/Environment/           # Brick, floor, spike materials
│   ├── Scripts/
│   │   ├── AI/                    # CharacterPersonalityTemplateManager, CapPersonalityPromptBuilder
│   │   ├── Dungeon/               # DungeonLevelBuilder, flavor, encounters
│   │   ├── Gameplay/              # Bootstrap, AiQuestPlanner, EnemyMeleeAI, quests, save, warm-up
│   │   ├── Player/                # PlayerCombat, health, inventory
│   │   ├── Settings/              # GameSettings, LlmPerformanceProfile (fast mode)
│   │   ├── UI/
│   │   └── OllamaHandler.cs
│   ├── Prompts/                   # cap_personality.j2, cap_context.json (runtime)
│   ├── SimpleOllamaInjection/
│   └── InputSystem_Actions.inputactions
└── ProjectSettings/
```

## 11. Troubleshooting

| Symptom | Likely cause | Fix |
|---|---|---|
| Yellow console: invalid GUID on `.meta` | Malformed 32-char GUID | Pull latest repo; let Unity reimport; do not hand-edit GUIDs to wrong length. |
| Cap / foe is a coloured capsule | FBX not assigned | Select **GameplaySystems** → assign `_npcModel` / `_enemyModel` or reimport `Assets/Models/`. |
| Models underground or huge | Scale / pivot | Tune `_npcHeight`, `_enemyHeight`, yaw offsets on `LevelGameplayBootstrap`. |
| Dialogue buttons do nothing | EventSystem / input asset | Scene should have `UiEventSystemBootstrap` via bootstrap; `InputSystem_Actions` on EventSystem. |
| Hear button → HTTP 404 | Model not pulled or wrong name | `ollama pull qwen3:4b`; match `OllamaHandler.defaultModel`. |
| Empty streamed reply | Stream / sanitize | Check Console; see `DialoguePanelController` + `OllamaHandler` NDJSON path. |
| `Cannot connect to localhost:11434` | Ollama not running | Start Ollama service. |
| Thinking tags in UI | Reasoning model | Output is sanitized; update `OllamaHandler` / `clearThinking` if leakage persists. |
| Spikes always damage | Not jumping over strip | Traps are narrow; damage only when feet are low (`HazardVolume`). |
| First LLM call very slow | Cold model | Wait on the Main Menu a few seconds (auto warm-up), enable **Fast AI responses**, or run `ollama run <model>` once before play. |
| Cap dialogue empty / prompt error | Template missing | Ensure `Assets/Prompts/cap_personality.j2` exists; check Console for `CharacterPersonalityTemplateManager` errors. |
| Voice line slow or empty | Cold model / queue | Wait for Main Menu warm-up; use **Another line**; enable **Fast AI responses**. |
| Cap shows planning text | Reasoning model (qwen3) | Enable **Fast AI responses** (`gemma3:4b`) or rely on `ExtractNpcSpokenDialogue`. |
| Console: `Request aborted HTTP 0` | Ollama contention | Fixed: request queue + planners defer while dialogue open. Close Cap and retry. |
| TMP missing glyph (e.g. ✓) | Unicode in UI text | Fixed: quest text and toggles use plain text / `Image` graphics instead of ✓. |
| Compile: Newtonsoft | Missing package | Install `com.unity.nuget.newtonsoft-json` or ignore legacy `OllamaRequester`. |

Save file location (Windows example): `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\dungeon_session_save.json` — exact path logged on **F5** in the Console.

## 12. Where to go next

- [`deliverables-checklist.md`](./deliverables-checklist.md) — full submission checklist.
- [`high-concept.md`](./high-concept.md) — design intent and scope.
- [`ollama-plan.md`](./ollama-plan.md) — LLM data flow and prompts.
- [`prompts-used.md`](./prompts-used.md) — prompt archive.
- [`llm-integration-report.md`](./llm-integration-report.md) — integration report.
- [`ethical-considerations.md`](./ethical-considerations.md) — AI transparency and opt-out.
- [`video-deliverables.md`](./video-deliverables.md) — required video shot lists.
- [`build-notes.md`](./build-notes.md) — export prototype/final build.
- [`art-direction.md`](./art-direction.md) — Meshy prompts and texture recipe.
- [`refinements-changes.md`](./refinements-changes.md) — change log.
- [`feedback.summary.md`](./feedback.summary.md) — consolidated playtest notes.
- [`critical.feedback.md`](./critical.feedback.md) — critical engagement with feedback.
- [`ai-usage-annexure.md`](./ai-usage-annexure.md) — assessment AI disclosure template.
