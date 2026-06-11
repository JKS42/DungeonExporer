# High Concept Document — DungeonExporer

> Design intent and scope for the current build.
> Last updated: 2026-06-11

## 1. Ideation

### Pitch (one sentence)

> *DungeonExporer* is a cosy 3D dungeon-exploration game where the dungeon's whimsical inhabitants — chatty NPCs, signs, and lore — are written in real time by a *local* large language model running on the player's own machine.

### Genre & perspective

- **Genre**: 3D dungeon crawler / exploration with light combat and quests.
- **Perspective**: **First-person** — `FirstPersonController` + child camera on the Player (`Assets/Scripts/Player/`).
- **Tone**: **Lighthearted fantasy.** Warm, whimsical, cosy. Visual palette: parchment, sun-gold, mossy green, cocoa, brass (`MenuTheme.cs`). LLM prompts enforce the same tone — no grimdark, no horror.

### Core loop (current Level1)

1. **Explore** a hybrid maze: narrow **`.`** corridors, large **S** safe rooms, central **E** encounter pits (`Level1_Maze.txt` → `DungeonLevelBuilder`).
2. **Meet Cap** in the nearest **S** hub to spawn; **E** to interact — accept quests; Cap's voice line appears automatically (prefetched Ollama). Type a question and **Ask Cap** for reactive replies.
3. **Fight** Meshy **DungeonFoe** creatures on **E** tiles (melee, left click); complete **Cap's corridor drill** (`defeated_dungeon_foe`).
4. **Survive** spike / ember / slime traps (jumpable; layout partly chosen by Ollama at load, validated in C#), pick up bubble loot (AI-suggested cells with procedural fill), read wooden **signs** in corridors, fight foes on **E** pits (AI placement + fill), and read HUD quest hints and flavor toasts on **S** / **E** zones.
5. **Return to Cap** for follow-up quest **Echoes in the dark** (stand on any **E** encounter volume).
6. **Save** session with **F5** / **F9** (`GameSaveService`) — position, quests, inventory.

### Win / fail conditions

*To be defined for a full game.* Level1 is a **vertical slice**: two quests, no formal win screen. Player death from hazards / future combat is supported via `PlayerHealth` (fade + respawn at save position when applicable).

### Save model (v1)

- **Session save** — `GameSaveService` writes `dungeon_session_save.json` to `Application.persistentDataPath` (player transform, `QuestManager` state, `PlayerInventory`). Auto-load on level start if the file exists; **F5** save, **F9** load.
- **Roguelite / permadeath** — possible later mode; not implemented.

### Out of scope (v1)

- Multiplayer.
- LLM-generated level geometry (maze ASCII stays authored; LLM may suggest trap *cells* on that grid, validated in C#).
- Voice synthesis (text only).
- Full enemy AI (foes are stationary damage targets).

## 2. The role of the LLM

The LLM is woven into gameplay as **flavor, dialogue, and trap placement hints**, with **authoritative quest facts and validation in C#**:

| Use case | Description | Status |
|---|---|---|
| NPC dialogue | Prefetched / auto-shown lines at Cap; **Ask Cap** reactive Q&A; quest facts from `QuestManager`. Turn memory via `NpcConversationMemory`. | **In Level1** |
| Trap layout | JSON plan at level load (`DungeonTrapPlanner`); cells validated (`IsTrapEligibleCell`); procedural fill for remainder. | **In Level1** |
| Loot / enemies / signs | JSON plan at level load (`DungeonContentPlanner`); loot on walkable tiles, foes on **E**, signs on **.** corridors; procedural fill. | **In Level1** |
| Room / tile flavor | Short lines when entering **S** or **E** volumes (`DungeonFlavorZone` → `DungeonFlavorNarrator` → HUD toast). | **In Level1** |
| Item / lore text | Descriptions on pickup or inspect. | Planned |
| Hint system | Context-aware hints. | Stretch |
| AI Director | Pacing / difficulty from play pattern. | Stretch |

Prompt structure, endpoints, and risks: [`ollama-plan.md`](./ollama-plan.md).

## 3. Why a *local* model is appropriate

1. **No per-call cost** — many generations per session.
2. **Offline play** — single-player expectation.
3. **Privacy** — prompts stay on the machine.
4. **No vendor rate limits** — Ollama on `localhost:11434`.
5. **Acceptable latency** — 1–3 s for short lines on modest hardware (`qwen3:4b`).
6. **Educational / portfolio value** — local LLM integration as a project pillar.

### Trade-offs

- Smaller models vs. cloud quality — mitigated by tight prompts and authored quest text.
- Player must install Ollama — mitigated by setup panel + [`setup.md`](./setup.md).
- Cold-start latency — warm-up planned (see `ollama-plan.md`).

## 4. Target platform / system requirements

- **OS**: Windows 10/11 (primary dev target). Linux/macOS possible for Ollama.
- **Minimum (draft)**: 16 GB RAM, modern CPU; integrated GPU OK for `qwen3:4b` on CPU.
- **Recommended**: 32 GB RAM, GPU with ≥ 8 GB VRAM for faster inference.

Measured specs: [`setup.md`](./setup.md).

## 5. Inspirations & references

*To be expanded.* Comparable ideas: cosy dungeon crawlers, AI-assisted narrative (AI Dungeon-style text, but local and grounded), classic grid dungeons with modern first-person presentation.
