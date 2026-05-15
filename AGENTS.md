# Agent Guidelines — DungeonExporer

Persistent guidance for any AI agent (Cursor, Copilot, etc.) working in this repo.

## Project at a glance

- **Type**: 3D dungeon exploration game.
- **Engine**: Unity `6000.3.8f1` with the Universal Render Pipeline (URP).
- **Distinctive feature**: a *local* LLM (via Ollama) drives in-game dialogue and flavor text; quests, maze layout, and combat are authoritative in C#.
- **Level1 slice**: hybrid ASCII maze, Cap NPC, scattered foes/loot/hazards, first-person combat, session save — see `README.md` and `docs/setup.md`.
- **Input**: Unity Input System (`InputSystem_Actions.inputactions`).
- **UI**: TextMesh Pro.

## Required documentation — keep these current

The following docs are part of the deliverable. If a change touches their topic, update them in the same change.

| File | Owns |
|---|---|
| `docs/deliverables-checklist.md` | Course submission mapping (brief → repo) |
| `docs/high-concept.md` | Game ideation, the LLM's role, justification for using a *local* model |
| `docs/ollama-plan.md` | Model choice, inference timing, data flow, prompt structure, risks |
| `docs/setup.md` | Install Ollama, pull models, system specs, run instructions |
| `docs/prompts-used.md` | Prompt archive (tested prompts, success/failure, iterations) |
| `docs/llm-integration-report.md` | 600–800 word integration report |
| `docs/ethical-considerations.md` | Transparency, licensing, crediting, player awareness |
| `docs/video-deliverables.md` | Recording guides for required videos |
| `docs/build-notes.md` | Prototype vs final build export |
| `docs/art-direction.md` | Style pillars, palette, per-asset prompts + tool settings (Meshy, etc.) |
| `docs/refinements-changes.md` | Continuous log of scope changes + AI-assisted decisions |
| `README.md` | Overview, install, dependencies, credits, AI tools used |

## Documentation update rules

1. **Every meaningful change** (new system, scope tweak, dependency swap, model change, prompt rewrite) gets an entry in `docs/refinements-changes.md` with:
   - Date (YYYY-MM-DD)
   - What changed
   - Why
   - Which AI tool assisted (e.g. "Cursor + Claude Opus 4.7", "ChatGPT-5", "Ollama:qwen3:4b")
2. **Scope changes** also update `docs/high-concept.md`.
3. **Model / prompt / data-flow changes** also update `docs/ollama-plan.md`.
4. **New dependency or package** updates `README.md` (Dependencies + Credits) and, if it affects install, `docs/setup.md`.
5. **New art asset** (3D model, texture, sprite) is recorded in `docs/art-direction.md` with the prompt + tool settings used to generate it. Place the asset under `Assets/Art/<Category>/<AssetName>/` with file names that match the asset, not the generator's defaults.
6. Never silently change behavior of the LLM pipeline without logging it in `refinements-changes.md`.

## Coding conventions

- **Namespaces**: `DungeonExporer.<Area>` (e.g. `DungeonExporer.Player`, `DungeonExporer.Dungeon`, `DungeonExporer.AI`).
- **Folders**: gameplay scripts under `Assets/Scripts/<Area>/` (`Dungeon/`, `Gameplay/`, `Player/`, `UI/`). Ollama gameplay client: `Assets/Scripts/OllamaHandler.cs` (consolidation with `SimpleOllamaUnity` planned). Meshy FBX imports live under `Assets/Models/` until moved to `Assets/Art/`.
- **One LLM client**: prefer `HardCodeDev.SimpleOllamaUnity.Ollama` going forward. Remove or wrap the duplicates (`OllamaHandler.cs`, `OllamaRequester.cs`) instead of growing them in parallel.
- **No secrets in git**: `Assets/Resources/Neocortex/NeocortexSettings.asset` currently contains a plaintext API key — rotate it and gitignore it before adding new secrets.

## When in doubt

If a request is ambiguous about scope (e.g. "add combat"), prefer a short plan in `refinements-changes.md` *before* implementing, so the decision is captured.
