# DungeonExporer

A cosy 3D dungeon-exploration game where NPC dialogue, room flavor, and quest banter are written in real time by a **local** large language model ([Ollama](https://ollama.com)) on the player's machine.

## Overview

- **Engine**: Unity `6000.3.8f1` (URP).
- **Perspective**: First-person (`FirstPersonController` + `CharacterController`).
- **Level1 slice**: Hybrid ASCII maze (`#` walls, `.` corridors, **S** safe hubs, **E** encounter pits) built at runtime by `DungeonLevelBuilder`; quest giver **Cap**, scattered loot and spike traps, Meshy **foes** on encounter tiles, melee combat, HUD, session save.
- **Local LLM**: Default model `qwen3:4b` (swappable on scene `OllamaHandler`).
- **Why local?** No per-call cost, offline play, privacy. See [`docs/high-concept.md`](docs/high-concept.md).

## Quick start

1. Install **Unity 6000.3.8f1** via Unity Hub.
2. Install **Ollama** from <https://ollama.com/download>.
3. Pull the default model:

```powershell
ollama pull qwen3:4b
```

4. Open this repo in Unity, open **`Assets/Scenes/Level1.unity`**, press **Play**.
5. If Ollama is down or the model is missing, an in-game setup panel links to [`docs/setup.md`](docs/setup.md); you can continue without streaming dialogue.
6. **Save**: **F5** writes `dungeon_session_save.json` under the OS persistent data path; **F9** reloads. A save auto-loads on level start when that file exists (delete it to reset).

Full install, controls, and troubleshooting: [`docs/setup.md`](docs/setup.md).

## Level1 gameplay (current)

| Action | Default binding (keyboard / mouse) |
|--------|-------------------------------------|
| Move / look | WASD + mouse |
| Jump | Space |
| Sprint | Left Shift |
| Crouch | C |
| Interact (Cap, UI) | **E** (hold where noted) |
| Attack (melee) | **Left click** (also Enter on keyboard) |
| Inventory panel | **I** |
| Pause | Escape |
| Save / load session | **F5** / **F9** |

**Suggested loop:** Find **Cap** in a green-tinted **S** safe room → accept **Cap's corridor drill** → defeat **DungeonFoe** creatures on crimson **E** floors (they chase and melee back; **left-click** to attack with hit sparks) → return to Cap → accept **Echoes in the dark** → stand on an **E** tile. Optional: **Ask Cap** typed questions (personality from `prompts/cap_personality.jinja2`); **Another line** for a new Ollama voice; bubble pickups; jump spike traps. Wait a few seconds on the **Main Menu** so Ollama can warm up before Level1.

## Documentation

| Document | Contents |
|---|---|
| [`docs/deliverables-checklist.md`](docs/deliverables-checklist.md) | **Submission checklist** (brief → repo files) |
| [`AGENTS.md`](AGENTS.md) | Conventions for AI agents working in this repo |
| [`docs/high-concept.md`](docs/high-concept.md) | Game concept, LLM role, save model, scope |
| [`docs/ollama-plan.md`](docs/ollama-plan.md) | Model choice, data flow, prompts, risks |
| [`docs/setup.md`](docs/setup.md) | Install, run, controls, troubleshooting |
| [`docs/prompts-used.md`](docs/prompts-used.md) | Prompt archive (success/failure, iterations) |
| [`docs/llm-integration-report.md`](docs/llm-integration-report.md) | Integration report (~780 words) |
| [`docs/ethical-considerations.md`](docs/ethical-considerations.md) | Transparency, licensing, player opt-out |
| [`docs/video-deliverables.md`](docs/video-deliverables.md) | Shot lists for required 3–6 min videos |
| [`docs/build-notes.md`](docs/build-notes.md) | Prototype vs final build export |
| [`docs/art-direction.md`](docs/art-direction.md) | Style pillars, asset prompts (Meshy, Pillow) |
| [`docs/refinements-changes.md`](docs/refinements-changes.md) | Running log of changes and AI-assisted decisions |

## Dependencies

### Runtime (Unity)

- Universal Render Pipeline (URP)
- TextMesh Pro
- Unity Input System (`Assets/InputSystem_Actions.inputactions`)
- Newtonsoft.Json (legacy `Assets/Ollama/OllamaRequester.cs` example)

### Local LLM

- [Ollama](https://ollama.com/) — `http://localhost:11434`
- Default: [`qwen3:4b`](https://ollama.com/library/qwen3)
- Optional: [`llama3`](https://ollama.com/library/llama3)
- **Python 3** + **Jinja2** — renders Cap dialogue prompts from [`prompts/cap_personality.jinja2`](prompts/cap_personality.jinja2) at runtime (`pip install jinja2`)

### Embedded .NET (SimpleOllamaUnity)

Under `Assets/SimpleOllamaInjection/Plugins/AIExtensions/` — `Microsoft.Extensions.AI`, hosting, configuration, etc. Powers `Assets/SimpleOllamaInjection/SimpleOllamaUnity/Ollama.cs` (preferred long-term client; gameplay still uses `OllamaHandler` for Level1).

### Art pipeline (optional, for regenerating textures)

- Python 3 + **Pillow** — `Tools/generate_dungeon_textures.py`

### Third-party in repo

- **SimpleOllamaUnity** (HardCodeDev) — `Assets/SimpleOllamaInjection/`
- **Neocortex** — `Assets/Resources/Neocortex/` *(under review; API key must not be committed)*

## AI tools used during development

Logged in [`docs/refinements-changes.md`](docs/refinements-changes.md); art prompts in [`docs/art-direction.md`](docs/art-direction.md).

| Tool | Used for |
|---|---|
| **Cursor** + Claude / GPT | Code, docs, debugging |
| **Ollama** (runtime) | NPC voice, Ask Cap Q&A, flavor toasts, level-load JSON plans |
| **Python + Jinja2** | Cap personality prompt template (`prompts/render_cap_prompt.py`) |
| **Meshy AI** | Player, Cap, Grumblemite FBX |
| **Python (Pillow)** | Tileable dungeon wall / floor / spike albedos |

## Project structure

```
DungeonExporer/
├── AGENTS.md
├── README.md
├── docs/
├── prompts/                      # cap_personality.jinja2, render_cap_prompt.py
├── Tools/
│   └── generate_dungeon_textures.py
├── Assets/
│   ├── Art/
│   │   ├── Characters/Adventurer/
│   │   └── Environment/          # DungeonBrick, DungeonFloor, SpikeTrap
│   ├── Models/                   # Meshy FBX: Cap (NPC), Grumblemite (foe)
│   ├── Scenes/                   # MainMenu.unity, Level1.unity
│   ├── Data/Dungeon/             # Level1_Maze.txt
│   ├── Scripts/
│   │   ├── Dungeon/              # Maze build, flavor zones, encounters
│   │   ├── AI/                   # CapPersonalityPromptBuilder (Jinja2)
│   │   ├── Gameplay/             # Quests, NPC, EnemyMeleeAI, loot, save, warm-up
│   │   ├── Player/               # Movement, PlayerCombat, health, inventory
│   │   ├── UI/                   # HUD, dialogue, menus, Ollama setup
│   │   ├── Settings/
│   │   ├── StreamingAssets/Prompts/  # Jinja2 mirror for standalone builds
│   │   └── OllamaHandler.cs
│   ├── SimpleOllamaInjection/
│   ├── Ollama/                   # Minimal legacy example
│   └── InputSystem_Actions.inputactions
└── ProjectSettings/
```

## Submission deliverables (course brief)

| Item | In repo? | Notes |
|---|---|---|
| Documentation pack | Yes | See table above + [`deliverables-checklist.md`](docs/deliverables-checklist.md) |
| Prototype (≥1 LLM feature) | Yes | Play Level1 in Editor |
| Final build (.exe) | **You export** | [`build-notes.md`](docs/build-notes.md) |
| Technical video (3–6 min) | **You record** | [`video-deliverables.md`](docs/video-deliverables.md) |
| Showcase video (3–6 min) | **You record** | Same |

## Status (Level1 vertical slice)

- [x] First-person movement, jump, crouch, sprint
- [x] ASCII maze + safe / encounter zones + textured walls / floors
- [x] Quest system (`QuestManager`) — Cap's drill + Echoes in the dark
- [x] NPC **Cap** (Meshy model) + dialogue UI + Ollama streaming
- [x] Two-way melee combat — `PlayerCombat` + `EnemyMeleeAI` (chase, attack) + `CombatHitVfx`
- [x] **Ask Cap** reactive Q&A (Jinja2 personality → Ollama)
- [x] Pickups (bubble + icon), spike hazards (jumpable), flavor narration on **S** / **E**
- [x] HUD (health, quest, inventory), pause menu, session save (F5/F9)
- [x] Ollama health check + in-game setup panel
- [ ] Consolidate Ollama clients (`OllamaHandler` → SimpleOllamaUnity)
- [ ] Player Adventurer mesh in scene (asset exists under `Art/Characters`)
- [ ] Rigged enemy animation, `RoomDefinition` / `NpcDefinition` ScriptableObjects
- [ ] Serialize concurrent Ollama requests (trap/content planners vs dialogue)
- [x] Boot-time Ollama warm-up (Main Menu via `OllamaMenuWarmup`)
- [ ] Full `GameSettings.LlmEnabled` kill-switch at all call sites

## Credits

- Game design & code: *<your name here>*
- [Unity](https://unity.com/) · [Ollama](https://ollama.com/) · [SimpleOllamaUnity](https://github.com/) *(add repo URL)*
- 3D characters: [Meshy AI](https://www.meshy.ai/) — prompts in [`docs/art-direction.md`](docs/art-direction.md)
- Environment albedos: procedural (Pillow) — see `Tools/generate_dungeon_textures.py`
- Documentation & code assistance: Cursor + Claude

## License

*To be decided.*
