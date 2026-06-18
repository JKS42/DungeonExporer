# Ollama Plan — DungeonExporer

> Living document. Update whenever the model, the prompt structure, or the data flow changes.
> Last updated: 2026-06-18 (Jinja2 Cap prompts, warm-up, sequential planners, playtest UX passes)

## 1. Model choice

### Candidate models

| Model | Size | Strengths | Weaknesses | Status |
|---|---|---|---|---|
| `qwen3:4b` | ~4 B params | Fast on modest hardware, currently wired into the test scenes via `OllamaHandler` and `SimpleOllamaUnity`. Reasoning model — emits `<think>` tags that we strip. | Smaller context window, weaker creative writing than larger models. | Default for development |
| `llama3` (8 B) | ~8 B params | Better prose for narration/dialogue. Default fallback in `OllamaRequester.cs`. | Heavier; may stall on integrated GPUs. | Fallback / quality tier |
| `phi3` / `mistral:7b` | 3–7 B | Alternatives to evaluate. | TBD | Not yet evaluated |

### Decision (current)

- **Default**: `qwen3:4b` (already the default in `OllamaHandler.cs` and `Test.cs`).
- **Quality tier (opt-in)**: `llama3` for players with bigger GPUs.
- The model name is read from a Unity `Inspector` field so it can be swapped without rebuilding.

### Decision criteria (when re-evaluating)

1. P95 token latency < 200 ms on the recommended spec.
2. Average response time for a 60-token narration < 2 s.
3. Subjective quality on a 20-prompt evaluation set (kept in `docs/eval/` — TODO).
4. Memory footprint under 8 GB VRAM (or 12 GB RAM in CPU mode).

## 2. Inference timing (targets)

| Use case | Max tokens | Target latency (P50) | Target latency (P95) | Streaming? |
|---|---|---|---|---|
| Room / tile flavor (safe vs encounter) | ~72 (`DungeonFlavorNarrator`) | < 2 s | < 5 s | No (single completion) |
| Full room prose (future) | 60 | < 1.5 s | < 3 s | Yes |
| NPC dialogue line | ~80 (`defaultNpcMaxTokens`) | < 2 s | < 4 s | No (prefetch + non-stream) |
| Reactive NPC Q&A (Ask Cap `/api/chat`) | ~160 (`defaultNpcChatMaxTokens`) | < 2 s | < 5 s | No |
| **Fast mode** (Options) | ~48 voice / ~80 chat; `gemma3:4b`; 2-sentence Cap prompts | < 1.5 s | < 3 s | No |
| Loot / enemy / sign JSON plan | ~240 | < 3 s | < 8 s | No |
| Item description (on pickup) | 40 | < 1 s | < 2 s | No (cache after first call) |
| Hint (on player request) | 100 | < 3 s | < 6 s | Yes |

> Numbers are *targets*, not measurements. Replace with measured numbers as soon as a benchmark scene exists.

### Pre-warming

The Ollama server keeps a model loaded for ~5 minutes after the last request. **Implemented:** `OllamaMenuWarmup` on the Main Menu calls `OllamaHandler.WarmupModelCoroutine` (8-token completion, `think: false`) when `GameSettings.LlmEnabled` is true, so Level1 dialogue/planning hits a hot model.

## 3. Data flow

### Implemented (Level1 slice)

```
OllamaFirstRunHealthCheck (Start)
   │
   ├─► GET /api/tags (reachable host + model tag substring match)
   └─► on failure: OllamaSetupPanelController → docs/setup.md; player may Continue without LLM

DungeonFlavorZone (S / E floor triggers, via DungeonLevelBuilder flavor volumes)
   │
   ▼
DungeonFlavorNarrator (cooldown, respects NarrationUiGate pause/dialogue flags)
   │  ──► OllamaHandler.RequestGeneration (capped num_predict)
   ▼
DungeonFlavorHudBridge → GameplayHudController.ShowFlavorToast

NpcInteractable (range + Interact / E)
   │
   ├─► proximity prefetch → NpcDialogueCache
   ▼
DialoguePanelController
   │  Cap prompts: CapPersonalityPromptBuilder + Python Jinja2 (prompts/cap_personality.jinja2)
   │  Authoritative: QuestManager title, briefing, objective hints, completionSummary
   │  Prompt context: inventory summary, quest state, NpcConversationMemory (player + Cap turns)
   │  UI: quest block + LLM block + player input + Ask Cap / Another line / Accept / Close
   │
   ├─► open: auto voice (cached or non-stream RequestGeneration, extractNpcDialogue)
   ├─► Ask Cap: reactive RequestGeneration with player question
   └─► Another line: invalidate cache + re-fetch

LevelGameplayBootstrap (Start)
   │
   ├─► PrefetchAiPlansSequential (trap plan, then content plan — one Ollama call at a time)
   │      DungeonTrapPlanner.FetchPlanCoroutine → traps JSON → ScatterTraps (when ready)
   │      DungeonContentPlanner.FetchPlanCoroutine → loot / enemies / signs JSON
   │      DungeonLootScatter.ScatterLoot + ScatterEnemies (AI first, procedural fill)
   │      DungeonSignPost.Create per validated sign cell

Parallel: OllamaHandler test UI on Level1 (manual prompt / stream for debugging)
```

**Not LLM-driven (authoritative C#):** maze layout (`Level1_Maze.txt`), quest objectives (`defeated_dungeon_foe`, `entered_encounter_zone`), combat, pickups, save/load.

### Planned (full pipeline)

```
Unity (gameplay event)
   │
   │ 1. Build PromptContext
   ▼
PromptBuilder  ──► systemPrompt + userPrompt strings
   │
   │ 2. SendMessage(OllamaRequest)
   ▼
SimpleOllamaUnity.Ollama  ──► HTTP POST http://localhost:11434/api/chat (stream)
   │
   │ 3. Token stream
   ▼
DialogueRenderer (UI)  ──► TextMesh Pro typewriter effect
   │
   │ 4. Final response stored back on the NPC / Room state
   ▼
WorldState  (for follow-up context)
```

The current `Assets/Scripts/OllamaHandler.cs` test path also appends each successful response to a JSON history file (`Assets/DialogueOutput/ollama-dialogue.json`) so the dialogue line can be reused by a later reader without depending on the live UI.

Key points:
- **All traffic is local** — `http://localhost:11434` only. The game never reaches a public endpoint.
- **State is owned by the game, not the LLM.** The LLM is stateless between calls; we re-send the relevant slice of world state each time.
- **Chat history is per-NPC.** Each NPC keeps its own short conversation history (last N turns), not a single global history.

## 4. Prompt structure

### Cap dialogue (Jinja2 — shipped)

**Canonical file:** [`prompts/cap_personality.jinja2`](../prompts/cap_personality.jinja2)  
**Runtime:** `CapPersonalityPromptBuilder` → `python prompts/render_cap_prompt.py` (Jinja2) → single user prompt string → `OllamaHandler.RequestGeneration`.

Modes:

| `mode` | When | Key context |
|---|---|---|
| `voice` | Panel open / **Another line** | `quest_title`, `quest_briefing`, `quest_state`, `inventory_summary`, `memory_block`, `situation` |
| `reactive` | **Ask Cap** | Above + `player_question` |

Personality macros (`cap_personality`, `cap_voice_rules`) live in the template — not duplicated in C#. Standalone builds mirror `prompts/` under `Assets/StreamingAssets/Prompts/`.

> Tone is locked to *lighthearted fantasy* (see `high-concept.md`). The template enforces cosy voice, sentence caps, and “reply with ONLY what Cap says out loud”.

### Other prompts (C# strings)

- **Flavor narrator** — `DungeonFlavorNarrator` (§ `prompts-used.md` §1.2).
- **Trap / content JSON** — `DungeonTrapPlanner` / `DungeonContentPlanner` maze block + JSON schema in prompt builders.

### Field sources

| Field | Source in code (Level1) |
|---|---|
| Cap persona / voice rules | `prompts/cap_personality.jinja2` (Jinja2 macros) |
| Quest title, briefing, state | `QuestManager` + `QuestDefinition` (not generated by LLM) |
| `inventory.summary` | `PlayerInventory.BuildSummaryForPrompt()` → Jinja2 context |
| Room / tile context | `DungeonFlavorKind` (safe vs encounter) for flavor narrator |
| `memory_block` | `NpcConversationMemory` (per `_npcConversationId`, e.g. `cap`) |
| Maze grid (trap/content plans) | `DungeonLevelBuilder.BuildMazePromptBlock()` |
| Future `room.*` | `RoomDefinition` ScriptableObject (planned) |

### Output constraints

- Strip `<think>...</think>` blocks (already implemented in `SimpleOllamaUnity.Ollama.ClearThinking`).
- `OllamaHandler.ExtractNpcSpokenDialogue` drops planning/meta lines (“We are building…”, “Quest title:”, etc.); dialogue coroutines do **not** fall back to raw model text.
- Trim to N sentences via the Jinja2 template / flavor prompt.
- If output is empty or whitespace, fall back to a hard-coded line (`DialoguePanelController` authored fallback).

## 5. Risks & mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Player doesn't have Ollama installed | High | Game can't start | First-run detector + friendly install instructions linking to `setup.md`. |
| First inference is slow (cold model) | High | Bad first impression | Warm-up call on Main Menu. |
| Model produces unsafe / off-brand output | Medium | Tonal breakage | Strict system prompt; profanity filter on output; cap tokens. |
| Model hallucinates inconsistent lore | High | Immersion break | Ground every prompt with canonical `RoomDefinition` / `NpcDefinition` text; cache "official" descriptions and only ask the LLM for *variations*. |
| `<think>`-tag leakage | Medium | UI shows reasoning text | `clearThinking = true` on every request; assert in code. |
| Ollama server crashes mid-session | Low | Stalled UI | Timeout on each request; fall back to canned text; surface a non-blocking toast. |
| Per-call latency budget blown on low-end hardware | Medium | Game feels stuttery | Run inference off the main thread (already async); show "..." indicator; allow disabling LLM features in settings. |
| Concurrent requests abort each other | **Mitigated at load** | Dialogue vs planners, or overlapping dialogue | Single-flight `OllamaHandler`; trap/content planners run sequentially at level load (2026-06-18); superseded aborts suppressed in logs; procedural fallback on timeout; full request queue still TODO. |
| Cap prompt render fails (no Python/Jinja2) | Medium | Empty Ask Cap / voice prompt | `pip install jinja2`; Python on PATH; see `docs/setup.md` troubleshooting. |
| API key for Neocortex committed in git | **Confirmed** | Account compromise | Rotate key; gitignore `NeocortexSettings.asset`; decide whether Neocortex stays in the project. |

## 6. Player-facing kill switch

`GameSettings.LlmEnabled` (toggle in the Options menu):

- When `true`, the dialogue system queries the model normally; flavor zones request narrator lines.
- When `false`, **Hear them out** shows a canned Cap line (`DialoguePanelController`); **DungeonFlavorNarrator** skips requests. Still TODO: centralize in `OllamaHandler` for the debug test UI.

The toggle defaults to `true` and persists via PlayerPrefs.

## 7. Open questions

- Do we keep Neocortex alongside Ollama, or remove it to keep the stack simple?
- Do we ship Ollama with the game (bundled), or require the player to install it themselves? (Currently: player installs.)
- Do we expose a *model picker* in Options, or keep the model name as an advanced text field? (Current Options panel exposes only the on/off toggle.)
- Should Cap prompts bundle a minimal Jinja2 renderer (no Python subprocess) for standalone builds on machines without Python?
