# High Concept Document — DungeonExporer

> A scaffold for the High Concept Document. Fill in / refine each section as design decisions are made.
> Last updated: 2026-05-14

## 1. Ideation

### Pitch (one sentence)

> *DungeonExporer* is a cosy 3D dungeon-exploration game where the dungeon's whimsical inhabitants — chatty NPCs, signs, and lore — are written in real time by a *local* large language model running on the player's own machine.

### Genre & perspective

- **Genre**: 3D dungeon crawler / exploration.
- **Perspective**: **First-person** — movement and look use a `CharacterController` plus child camera (`Assets/Scripts/Player/FirstPersonController.cs`).
- **Tone**: **Lighthearted fantasy.** Warm, whimsical, cosy. Think: friendly tavern at the entrance, mushroom-lit caverns, NPCs that crack jokes, danger that's silly more often than grim. Visual palette is warm parchment + sun-gold + mossy green (codified in `Assets/Scripts/UI/MenuTheme.cs`). LLM system prompts must respect this tone — no grimdark, no horror.

### Core loop (draft)

1. Enter a room (layout includes **safe** rest areas and **encounter** zones the dungeon builder marks in-world).
2. Observe environment + interact with NPCs/objects.
3. **Level1 slice**: talk to the placeholder NPC (**E** / Interact), accept a **quest** from `QuestManager`, optionally hear extra lines from **Ollama** (`DialoguePanelController` → `OllamaHandler.RequestGenerationStreaming` with NDJSON + typewriter reveal), beat the training dummy, then (after Cap’s drill) accept **Echoes in the dark** and step on any **E** encounter tile to finish it. Broader: NPC dialogue, item descriptions, and room flavor from the local LLM, conditioned on world state.
4. Solve / fight / loot (**HP**, hazards, **inventory** pickups on Level1).
5. Move to the next room. Loop.

### Win / fail conditions

*To be defined.* Likely a "reach the bottom floor" or "defeat the boss" goal, with permadeath optional.

### Out of scope (v1)

- Multiplayer.
- Procedural mesh / level geometry generation by the LLM (LLM produces *text and behavior hints*, not geometry).
- Voice synthesis (text only for v1, TTS is a stretch goal).

## 2. The role of the LLM

The LLM is **not** a chatbot bolted onto the game — it is woven into the gameplay layer in these specific ways:

| Use case | Description | Status |
|---|---|---|
| NPC dialogue | LLM generates NPC responses conditioned on NPC persona + world state. | **Partial** — first NPC uses `DialoguePanelController` + `OllamaHandler.RequestGeneration`; quest facts remain authoritative in C#. |
| Room narration | When the player enters a room, the LLM produces a short flavor description. | Planned |
| Item / lore text | Descriptions of items and lore fragments are LLM-generated. | Planned |
| Hint system | Optional context-aware hints when the player is stuck. | Stretch |
| AI Director (stretch) | LLM influences encounter difficulty/pacing based on the player's recent play pattern. | Stretch |

For each use case, the prompt structure, inputs, and grounding rules are detailed in [`ollama-plan.md`](./ollama-plan.md).

## 3. Why a *local* model is appropriate

A local model (run via [Ollama](https://ollama.com/)) is the right choice for this project for the following reasons:

1. **No per-call cost** — generation happens dozens of times per play session; cloud API calls would make playtesting and shipping the game expensive or impossible.
2. **Offline play** — the game can be played without an internet connection, which matches a single-player dungeon crawler's expectations.
3. **Privacy** — no player prompts or world state ever leaves the player's machine.
4. **No rate limits / no vendor lock-in** — the game is not at the mercy of an external service changing terms, pricing, or availability.
5. **Latency budget is acceptable on local hardware** — most use cases (room narration, dialogue) can tolerate 1–3 s, which a small local model on a modern GPU/CPU can meet. See `ollama-plan.md` for measured numbers.
6. **Educational value** — local-LLM integration is the project's distinctive technical contribution.

### Trade-offs accepted

- **Smaller models, lower output quality** vs. frontier cloud models — mitigated by tight prompting + retrieval of canonical lore.
- **System requirements** — the player needs to install Ollama and have enough RAM/VRAM for the chosen model. Documented in `setup.md`.
- **Cold-start latency** — first inference loads the model into memory. We will warm the model at game launch.

## 4. Target platform / system requirements (draft)

- **OS**: Windows 10/11 (primary). Linux/macOS as a stretch since Ollama supports both.
- **Min spec (draft)**: 16 GB RAM, modern CPU, integrated GPU acceptable for ~4B parameter models.
- **Recommended**: 32 GB RAM, dedicated GPU with ≥ 8 GB VRAM for snappier inference.

Final specs to be measured and recorded in `setup.md` once a model is locked in.

## 5. Inspirations & references

*To be filled in — list comparable games (e.g. AI Dungeon, Inkle's titles, classic Roguelikes) and what we are / aren't borrowing.*
