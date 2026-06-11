# Setup Guide — DungeonExporer

> Install, run, and playtest the current Level1 slice.
> Last updated: 2026-05-15

## 1. System requirements

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

## 2. Tooling

| Tool | Version | Purpose |
|---|---|---|
| [Unity Hub](https://unity.com/download) | latest | Editor installs |
| Unity Editor | **6000.3.8f1** (`ProjectSettings/ProjectVersion.txt`) | Engine |
| [Git](https://git-scm.com/) | latest | Source control |
| [Ollama](https://ollama.com/download) | latest | Local LLM |
| Python 3 + Pillow | optional | Regenerate dungeon textures (`Tools/generate_dungeon_textures.py`) |
| IDE | Visual Studio 2022 / Rider / VS Code | C# |

## 3. Clone the repo

```powershell
git clone https://github.com/<your-org>/DungeonExporer.git
cd DungeonExporer
```

## 4. Install Ollama

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

## 5. Pull the required model(s)

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

The scene **`OllamaHandler`** component's **Default Model** must match a name from `ollama list` exactly (e.g. `qwen3:4b`).

## 6. Open the project in Unity

1. Unity Hub → **Add** → select the repo folder.
2. Install editor **6000.3.8f1** if prompted.
3. First import may take several minutes (Meshy FBX under `Assets/Models/`, environment textures).

### First-time mesh / material checks

- **Cap / foes**: `GameplaySystems` → `LevelGameplayBootstrap` should reference FBX under `Assets/Models/` (`_npcModel`, `_enemyModel`). If empty, select the component in the editor — paths auto-assign via `GameplayModelPaths`.
- **Dungeon**: `Dungeon` object → `_wallMaterial`, `_floorMaterial` on `DungeonLevelBuilder`.
- **Spikes**: `GameplaySystems` → `_spikeTrapMaterial`.
- **Adventurer** (optional): extract materials from `Assets/Art/Characters/Adventurer/Adventurer.fbx` — see [`art-direction.md`](./art-direction.md).

## 7. Run Level1

1. Open `Assets/Scenes/Level1.unity`.
2. Ensure Ollama is running.
3. Press **Play**.

### Boot flow

- **`OllamaFirstRunHealthCheck`**: pings Ollama and checks the model tag; on failure, **`OllamaSetupPanelController`** offers a link to this guide and **Continue** (play without streaming).
- **`DungeonLevelBuilder`**: builds maze from `Assets/Data/Dungeon/Level1_Maze.txt`, places player at **P**.
- **`LevelGameplayBootstrap`**: spawns **Cap**, scatters pebbles, rations, spike traps, and foes on **E** cells.
- **`GameSaveService`**: auto-loads `dungeon_session_save.json` if present.

### Controls (keyboard & mouse)

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

The **Dungeon** object builds **5.5m** stone walls, a **ceiling** on every walkable cell, and **torch lighting** (warm point lights + boosted sun/ambient). Tune on **Dungeon** → `DungeonLevelBuilder`: `_wallHeight`, `_torchIntensity`, `_maxTorches`, `_directionalIntensity`.

### Playtest checklist

1. Find **Cap** (safe **S** room near spawn).
2. **E** → accept **Cap's corridor drill**.
3. Optional: **Hear them out** — streamed Ollama line in the dialogue panel.
4. Go to crimson **E** floors; **left-click** a **DungeonFoe** until it dies (~2 hits).
5. Return to Cap to complete the quest; accept **Echoes in the dark** if offered.
6. Step onto an **E** encounter tile (trigger volume) for the second quest.
7. Walk through bubble pickups; jump narrow spike strips.

### Ollama tester UI

Level1 still includes an on-screen **Ollama** test panel (`OllamaHandler`) for raw prompt/stream debugging alongside gameplay dialogue.

## 8. Regenerate environment textures (optional)

```powershell
pip install Pillow
python Tools/generate_dungeon_textures.py
```

Reimports: `DungeonBrick_Albedo.png`, `DungeonFloor_Albedo.png`, `SpikeTrap_Albedo.png`.

## 9. Project structure (current)

```
DungeonExporer/
├── AGENTS.md
├── README.md
├── docs/
├── Tools/generate_dungeon_textures.py
├── Assets/
│   ├── Scenes/MainMenu.unity, Level1.unity
│   ├── Data/Dungeon/Level1_Maze.txt
│   ├── Models/                    # Meshy: Cap + Grumblemite FBX
│   ├── Art/Characters/Adventurer/
│   ├── Art/Environment/           # Brick, floor, spike materials
│   ├── Scripts/
│   │   ├── Dungeon/               # DungeonLevelBuilder, flavor, encounters
│   │   ├── Gameplay/              # Bootstrap, quests, NPC, enemies, save
│   │   ├── Player/
│   │   ├── UI/
│   │   └── OllamaHandler.cs
│   ├── SimpleOllamaInjection/
│   └── InputSystem_Actions.inputactions
└── ProjectSettings/
```

## 10. Troubleshooting

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
| First LLM call very slow | Cold model | Wait on the Main Menu a few seconds (auto warm-up), or run `ollama run <model>` once before play. |
| Compile: Newtonsoft | Missing package | Install `com.unity.nuget.newtonsoft-json` or ignore legacy `OllamaRequester`. |

Save file location (Windows example): `%USERPROFILE%\AppData\LocalLow\<CompanyName>\<ProductName>\dungeon_session_save.json` — exact path logged on **F5** in the Console.

## 11. Where to go next

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
