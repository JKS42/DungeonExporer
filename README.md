# DungeonExporer

A 3D dungeon-exploration game where the dungeon's inhabitants, signs, and lore are generated in real time by a **local** large language model (via [Ollama](https://ollama.com)) running on the player's own machine.

> 🚧 Early development. Pieces of the LLM pipeline are wired up; gameplay systems (player, dungeon, combat) have not been built yet.

## Overview

- **Engine**: Unity `6000.3.8f1` (URP).
- **Perspective**: 3D dungeon crawler. First- or third-person — TBD.
- **Local LLM**: Ollama with `qwen3:4b` as the default model (swappable).
- **Why local?** No per-call cost, offline play, privacy, no rate limits. See [`docs/high-concept.md`](docs/high-concept.md) for the full rationale.

## Quick start

1. Install **Unity 6000.3.8f1** via Unity Hub.
2. Install **Ollama** from <https://ollama.com/download>.
3. Pull the default model:

```powershell
ollama pull qwen3:4b
```

4. Open this project in Unity, open `Assets/Scenes/Level1.unity`, and press **Play**.

Full step-by-step instructions in [`docs/setup.md`](docs/setup.md).

## Documentation

| Document | Contents |
|---|---|
| [`AGENTS.md`](AGENTS.md) | Conventions for AI agents working in this repo |
| [`docs/high-concept.md`](docs/high-concept.md) | Game concept, role of the LLM, why local |
| [`docs/ollama-plan.md`](docs/ollama-plan.md) | Model choice, inference timing, data flow, prompt structure, risks |
| [`docs/setup.md`](docs/setup.md) | Full install & run guide, system specs |
| [`docs/refinements-changes.md`](docs/refinements-changes.md) | Running log of changes and AI-assisted decisions |

## Dependencies

### Runtime (Unity packages & built-in)

- Universal Render Pipeline (URP)
- TextMesh Pro
- Unity Input System
- Newtonsoft.Json (used by `Assets/Ollama/OllamaRequester.cs`)

### Local LLM stack

- [Ollama](https://ollama.com/) — local LLM server (HTTP on `localhost:11434`).
- Default model: [`qwen3:4b`](https://ollama.com/library/qwen3).
- Optional quality-tier model: [`llama3`](https://ollama.com/library/llama3).

### Embedded .NET assemblies (under `Assets/SimpleOllamaInjection/Plugins/AIExtensions/`)

- `Microsoft.Extensions.AI` + `Microsoft.Extensions.AI.Ollama`
- `Microsoft.Extensions.Hosting` / `DependencyInjection` / `Logging` / `Configuration`
- Supporting `System.*` assemblies

These power the preferred Ollama client at `Assets/SimpleOllamaInjection/SimpleOllamaUnity/Ollama.cs`.

### Third-party AI SDKs in the repo

- **SimpleOllamaUnity** by HardCodeDev — `Assets/SimpleOllamaInjection/`.
- **Neocortex** AI-NPC SDK — `Assets/Resources/Neocortex/` *(under review; may be removed)*.

## AI tools used during development

This project uses AI assistance throughout. Every meaningful AI-assisted decision is logged in [`docs/refinements-changes.md`](docs/refinements-changes.md).

| Tool | Used for |
|---|---|
| **Cursor IDE** + **Claude Opus 4.7** | Code generation, refactoring, documentation drafting |
| **Ollama** (runtime, in-game) | Generates NPC dialogue, room narration, item lore — the game itself |
| *(add others as we use them: ChatGPT, Copilot, etc.)* | |

## Project structure

```
DungeonExporer/
├── AGENTS.md
├── README.md
├── docs/
├── Assets/
│   ├── Scenes/                # MainMenu, Level1
│   ├── Scripts/               # Gameplay code (mostly empty so far)
│   ├── Ollama/                # Minimal Ollama example
│   ├── SimpleOllamaInjection/ # Preferred Ollama wrapper + plugin DLLs
│   ├── Resources/Neocortex/   # Neocortex SDK settings (under review)
│   └── ...
└── ProjectSettings/
```

## Status

- [x] Unity project skeleton
- [x] Ollama integration (3 client variants — to be consolidated)
- [x] Documentation scaffolding
- [ ] 3D player controller
- [ ] First playable dungeon room
- [ ] `RoomDefinition` / `NpcDefinition` data
- [ ] LLM-driven NPC dialogue system
- [ ] Combat
- [ ] Inventory
- [ ] Save / load

## Credits

- Game design & code: *<your name here>*
- Engine: [Unity Technologies](https://unity.com/)
- Local LLM runtime: [Ollama](https://ollama.com/)
- Ollama Unity wrapper: [SimpleOllamaUnity by HardCodeDev](https://github.com/) *(replace with the actual link when known)*
- AI-NPC SDK: [Neocortex](https://neocortex.link/) *(under review)*
- Documentation & code assistance: Cursor + Claude Opus 4.7

## License

*To be decided.*
