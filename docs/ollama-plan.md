# Ollama Plan — DungeonExporer

> Living document. Update whenever the model, the prompt structure, or the data flow changes.
> Last updated: 2026-05-15 (Level1 gameplay wiring, streaming dialogue, health check)

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
| NPC dialogue line | ~96 (`num_predict` default on `OllamaHandler`) | < 2 s | < 4 s | Yes |
| Item description (on pickup) | 40 | < 1 s | < 2 s | No (cache after first call) |
| Hint (on player request) | 100 | < 3 s | < 6 s | Yes |

> Numbers are *targets*, not measurements. Replace with measured numbers as soon as a benchmark scene exists.

### Pre-warming

The Ollama server keeps a model loaded for ~5 minutes after the last request. To avoid first-prompt latency, the game will issue a tiny warm-up prompt at boot (e.g. on the Main Menu).

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
   ▼
DialoguePanelController
   │  Authoritative: QuestManager title, briefing, objective hints, completionSummary
   │  Prompt context: inventory summary, quest state, NpcConversationMemory (per npc id, e.g. "cap")
   │  UI: quest block + separate LLM block; Player map disabled while open
   │  Buttons: Accept quest, Hear them out, Close (UiEventSystemBootstrap + direct raycast fallback)
   │
   │ optional: "Hear them out"
   ▼
OllamaHandler.RequestGenerationStreaming (preferred in dialogue)
   │  POST http://localhost:11434/api/generate  (UnityWebRequest + NDJSON DownloadHandler)
   │  stream:true → per-line JSON; deltas appended to visible TMP + typewriter buffer
   │  SanitizeModelOutput: strip <think>, redacted_thinking, etc.
   │  AbortActiveRequest on close / new line
   │  On complete: NpcConversationMemory records assistant line
   ▼
Optional: append to Assets/DialogueOutput/ollama-dialogue.json (test / debug history)

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

### Template (work in progress)

> Tone is locked to *lighthearted fantasy* (see `high-concept.md`). System prompts must enforce this — no grimdark, no horror, no profanity.

```
[SYSTEM]
You are {npc.name}, a {npc.role} in a cosy, whimsical dungeon.
Personality: {npc.traits}.
Tone: warm, friendly, lightly humorous. Never grim, never crude.
You always answer in 1-3 sentences. Never break character.
Never mention that you are an AI.

[CONTEXT]
Room: {room.id} — {room.shortDescription}
Player carries: {inventory.summary}
Recent events: {worldState.recentEvents (last 3)}

[HISTORY]
{npc.lastTurns (max 4)}

[USER]
{player.input}
```

### Field sources

| Field | Source in code (Level1) |
|---|---|
| `npc.name` / persona | `NpcInteractable` + `NpcPromptRegistry` / hard-coded Cap prompts |
| Quest title, briefing, state | `QuestManager` + `QuestDefinition` (not generated by LLM) |
| `inventory.summary` | `PlayerInventory` via dialogue prompt builder |
| Room / tile context | `DungeonFlavorKind` (safe vs encounter) for flavor narrator |
| `npc.lastTurns` | `NpcConversationMemory` (per `_npcConversationId`, e.g. `cap`) |
| Future `room.*` | `RoomDefinition` ScriptableObject (planned) |

### Output constraints

- Strip `<think>...</think>` blocks (already implemented in `SimpleOllamaUnity.Ollama.ClearThinking`).
- Trim to N sentences server-side via the system prompt.
- If output is empty or whitespace, fall back to a hard-coded line.

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
