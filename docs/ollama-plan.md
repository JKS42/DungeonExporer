# Ollama Plan — DungeonExporer

> Living document. Update whenever the model, the prompt structure, or the data flow changes.
> Last updated: 2026-06-18 (AI side quests, Ask Cap removed, quest completion HUD, planning filters)

## 1. Model choice

### Candidate models

| Model | Size | Strengths | Weaknesses | Status |
|---|---|---|---|---|
| `qwen3:4b` | ~4 B params | Default in `GameSettings.LlmModel`; good for development. Reasoning model — may leak planning text (filtered in C#). | Weaker creative writing; needs `ExtractNpcSpokenDialogue` / flavor extraction. | Default (normal mode) |
| `gemma3:4b` | ~4 B params | Faster, fewer planning leaks; used when **Fast AI responses** is on. | Slightly less nuanced than larger models. | Fast-mode default |
| `llama3` (8 B) | ~8 B params | Better prose for narration/dialogue. | Heavier; may stall on integrated GPUs. | Optional quality tier |

### Decision (current)

- **Normal mode**: `qwen3:4b` via `GameSettings.LlmModel` (PlayerPrefs).
- **Fast mode** (Options → **Fast AI responses**): `gemma3:4b`, shorter Cap prompts (2 sentences), lower `num_predict` caps, trimmed chat memory.
- **Inspector fallback**: `OllamaHandler.defaultModel` is `gemma3:4b` when settings are empty.

### Decision criteria (when re-evaluating)

1. P95 token latency < 200 ms on the recommended spec.
2. Average response time for a 60-token narration < 2 s.
3. Subjective quality on a 20-prompt evaluation set (kept in `docs/eval/` — TODO).
4. Memory footprint under 8 GB VRAM (or 12 GB RAM in CPU mode).

## 2. Inference timing (targets)

| Use case | Max tokens | Target latency (P50) | Target latency (P95) | Streaming? |
|---|---|---|---|---|
| Room / tile flavor (safe vs encounter) | ~72 (`DungeonFlavorNarrator`) | < 2 s | < 5 s | No |
| NPC voice line | ~80 (`defaultNpcMaxTokens`) | < 2 s | < 4 s | No (prefetch + non-stream) |
| AI side quest JSON (`AiQuestPlanner`) | ~320 | < 3 s | < 8 s | No |
| **Fast mode** (Options) | ~48 voice; `gemma3:4b`; 2-sentence Cap prompts | < 1.5 s | < 3 s | No |
| Loot / enemy / sign JSON plan | ~240 | < 3 s | < 8 s | No |
| Main Menu warm-up | 8 | < 1 s | < 2 s | No |

> Numbers are *targets*, not measurements. Replace with measured numbers as soon as a benchmark scene exists.

### Pre-warming

**Implemented:** `OllamaMenuWarmup` on the Main Menu calls `OllamaHandler.WarmupModelCoroutine` when `GameSettings.LlmEnabled` is true. Re-warms when Options change LLM or fast-mode settings; skips duplicate warm-ups for the same model tag.

## 3. Data flow

### Implemented (Level1 slice)

```
OllamaFirstRunHealthCheck (Start)
   │
   ├─► GET /api/tags (reachable host + model tag substring match)
   └─► on failure: OllamaSetupPanelController → docs/setup.md; player may Continue without LLM

OllamaHandler (all gameplay calls)
   │
   └─► FIFO request queue (one call at a time)
         ├─► /api/generate (non-stream): voice, flavor, trap/content JSON, AI side quests
         └─► /api/generate (stream): Level1 debug test UI only

DungeonFlavorZone (S / E floor triggers)
   │
   ▼
DungeonFlavorNarrator → RequestGeneration → ExtractFlavorLine → HUD toast

NpcInteractable (range + Interact / E)
   │
   ├─► proximity prefetch → NpcDialogueCache
   ▼
DialoguePanelController
   │  Cap prompts: CharacterPersonalityTemplateManager + Assets/Prompts/cap_personality.j2
   │  Authoritative: QuestManager title, briefing, objective hints, completionSummary
   │  UI: quest block + LLM voice block + Another line / Accept / Close (no player text input)
   │
   ├─► open: instant canned fallback → auto voice (cached or RequestGeneration)
   └─► Another line: invalidate cache + re-fetch voice

QuestManager
   │
   ├─► NotifyWorldEvent(objective id) — advances active quests
   └─► QuestCompleted → GameplayHudController toast + objective-line banner

LevelGameplayBootstrap (Start)
   │
   ├─► AiQuestPlanner.RegisterFallbackQuests (immediate; save-safe)
   ├─► Wait 4s (Cap chat window at spawn)
   ├─► PrefetchAiPlansSequential — trap → content → AI side quests; each waits until dialogue closed
   │      DungeonTrapPlanner / DungeonContentPlanner → JSON → validated scatter + procedural fill
   │      AiQuestPlanner → JSON side quests (ai_cap_side_a/b) → QuestWorldEvents validation → QuestManager
   │      DungeonSignPost.Create per validated sign cell

Parallel: OllamaHandler test UI on Level1 (manual prompt / stream for debugging)
```

**Not LLM-driven (authoritative C#):** maze layout (`Level1_Maze.txt`), quest **objective event ids** (`QuestWorldEvents`), combat, save/load. Side-quest *flavor text* is LLM-generated but objectives must match the whitelist.

Key points:
- **All traffic is local** — `http://localhost:11434` only.
- **State is owned by the game, not the LLM.** Each request rebuilds context from quest/inventory/memory state.
- **Cap dialogue is one-way** — no player-typed questions in the shipped UI (removed 2026-06-18).

## 4. Prompt structure

### Cap dialogue (C# template — shipped)

**Runtime file:** `Assets/Prompts/cap_personality.j2`  
**Context defaults:** `Assets/Prompts/cap_context.json`  
**Renderer:** `CharacterPersonalityTemplateManager` (DatingSim-style `{{ field }}` replacement) via `CapPersonalityPromptBuilder`.

**Legacy (offline only):** `prompts/cap_personality.jinja2` + `prompts/render_cap_prompt.py` — not invoked at runtime.

| Mode | When | Key context |
|---|---|---|
| `voice` | Panel open / **Another line** | `quest_title`, `quest_briefing`, `quest_state`, `inventory_summary`, `memory_block`, `situation` |

> Tone is locked to *lighthearted fantasy* (see `high-concept.md`).

### AI side quests (`AiQuestPlanner`)

JSON schema with fixed ids `ai_cap_side_a` and `ai_cap_side_b`. Objectives must be chosen from `QuestWorldEvents` catalog (e.g. `defeated_dungeon_foe`, `collected_pebble`, `entered_safe_room`). C# validates and registers on `QuestManager`; fallback quests register if Ollama is off or JSON fails.

### Other prompts (C# strings)

- **Flavor narrator** — `DungeonFlavorNarrator` (see `prompts-used.md` §1.2).
- **Trap / content JSON** — `DungeonTrapPlanner` / `DungeonContentPlanner` maze block + JSON schema.

### Field sources

| Field | Source in code (Level1) |
|---|---|
| Cap persona / voice rules | `Assets/Prompts/cap_personality.j2` |
| Quest title, briefing, state | `QuestManager` + `QuestDefinition` (main + dynamic AI quests) |
| `inventory_summary` | `PlayerInventory.BuildSummaryForPrompt()` |
| Room / tile context | `DungeonFlavorKind` for flavor narrator |
| `memory_block` | `NpcConversationMemory` (Cap voice turns) |
| Maze grid (trap/content plans) | `DungeonLevelBuilder.BuildMazePromptBlock()` |

### Output constraints

- `think: false` on gameplay requests where supported.
- `SanitizeModelOutput`, `ExtractNpcSpokenDialogue`, `ExtractFlavorLine` strip planning/meta text.
- Empty voice output → authored fallback in `DialoguePanelController`.

## 5. Risks & mitigations

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Player doesn't have Ollama installed | High | No LLM voice | First-run detector + setup panel; **Continue** without LLM. |
| First inference is slow (cold model) | High | Bad first impression | Main Menu warm-up; **Fast AI responses** option. |
| Model produces unsafe / off-brand output | Medium | Tonal breakage | Strict system prompt; token caps; player opt-out. |
| qwen3 planning text in UI | Medium | Broken immersion | Extraction filters; fast mode uses `gemma3:4b`. |
| Ollama contention (HTTP 0) | **Mitigated** | Empty Cap / planner timeout | FIFO request queue; planners defer 4s + while dialogue open. |
| Invalid AI quest objectives | Medium | Uncompletable quests | `QuestWorldEvents` whitelist + fallback side quests. |
| Cap template missing | Low | Empty prompt | Ship `Assets/Prompts/` in build; log errors from `CharacterPersonalityTemplateManager`. |
| API key for Neocortex committed in git | **Confirmed** | Account compromise | Rotate key; gitignore `NeocortexSettings.asset`. |

## 6. Player-facing controls

**`GameSettings.LlmEnabled`** (Options → **AI-driven dialogue (Ollama)**):

- When `true`: Cap voice, flavor, AI side quests, and planners run when Ollama is available.
- When `false`: opening Cap shows an authored canned line; **Another line** is hidden; flavor and Ollama planners are skipped (procedural fill + fallback side quests only).

**`GameSettings.LlmFastMode`** (Options → **Fast AI responses**):

- Uses `gemma3:4b`, lower token caps, shorter Cap prompts, trimmed chat memory.

Both persist via PlayerPrefs. Main Menu warm-up re-runs when these change.

## 7. Open questions

- Consolidate `OllamaHandler` with SimpleOllamaUnity?
- Ship a bundled Ollama installer vs. player installs separately? (Currently: player installs.)
- Expose model picker in Options beyond fast/normal presets?
- Re-introduce player Q&A with stricter UX (removed from shipped UI 2026-06-18)?
