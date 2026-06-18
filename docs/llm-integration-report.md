# LLM Integration Report — DungeonExporer

> Word count: ~790 (body).  
> Last updated: 2026-06-18

## 1. Introduction

*DungeonExporer* is a first-person dungeon slice built in Unity 6000.3.8f1. Its distinguishing feature is optional narrative and layout flavor from a **local** large language model through [Ollama](https://ollama.com), bound to `http://localhost:11434`. The game remains fully playable when Ollama is missing: quests, combat, traps, and UI use authored C# content. The LLM adds atmosphere, NPC voice, and trap placement suggestions — not core progression logic.

This report summarizes technical decisions, integration strategy, performance considerations, gameplay impact, playtest-driven iteration, and how AI tools shaped the development workflow. Critical engagement with reviewer feedback is documented separately in [`critical.feedback.md`](./critical.feedback.md); raw reviewer notes are in [`feedback.summary.md`](./feedback.summary.md).

## 2. Technical decisions

### 2.1 Why Ollama and a local model

We chose Ollama over cloud APIs for four reasons aligned with the project brief: **no per-token cost** during iteration and playtesting; **offline play** after the model is pulled; **privacy** (prompts stay on the player machine); and **pedagogical clarity** for demonstrating integration in a technical video. The default PlayerPrefs model is **`qwen3:4b`**; **`gemma3:4b`** is the `OllamaHandler` inspector fallback for reliable in-character speech on modest hardware. **`llama3`** is an optional quality tier on stronger GPUs. Planning-text leakage from reasoning models is mitigated in post-processing, not only by model choice.

### 2.2 Client architecture

Gameplay uses `OllamaHandler` (`Assets/Scripts/OllamaHandler.cs`) with UnityWebRequest and two endpoints: **`/api/generate`** for flavor/planner paths and **`/api/chat`** for Ask Cap player conversation. NPC dialogue uses **non-streaming** completions with `think: false`, JSON-capable requests for trap planning, and optional NDJSON streaming for the scene test UI. Zone flavor uses a separate non-streaming call with a lower `num_predict` cap. **SimpleOllamaUnity** is included for future consolidation but is not yet the primary path.

Reasoning models may leak planning text or put tokens in a separate `thinking` field; we send **`think: false`** on gameplay requests, fall back to the `thinking` stream when `response` is empty (qwen3), and sanitize outputs (`SanitizeModelOutput`, `ExtractNpcSpokenDialogue`). An abort path cancels in-flight requests when the dialogue panel closes.

### 2.3 Authoritative game state vs. stateless LLM

The LLM is **stateless between calls**. Each request rebuilds context from:

- `QuestDefinition` (title, briefing — not generated),
- `QuestManager.BuildPromptContext()` (active/completed quests),
- `PlayerInventory.BuildSummaryForPrompt()`,
- `NpcConversationMemory` (last few assistant lines per NPC id),
- `DungeonLevelBuilder.BuildMazePromptBlock()` (ASCII maze for trap planning).

Quest acceptance, objectives (`defeated_dungeon_foe`, `entered_encounter_zone`), trap **validation** (walkable cells, distance from spawn, no safe-room placement), and rewards are **never** parsed blindly from model output. This “facts in code, flavor in model” split limits hallucination risk and keeps marking/replay deterministic.

### 2.4 Boot-time health check

`OllamaFirstRunHealthCheck` queries `/api/tags` and verifies the configured model name exists. Failure opens `OllamaSetupPanelController`, linking to `docs/setup.md`, with **Continue** so players without Ollama can still play. This addresses the highest-likelihood deployment risk (Ollama not installed) without hard-blocking the executable.

## 3. Integration strategy

Six LLM touchpoints ship in Level1:

1. **NPC dialogue** — `NpcInteractable` prefetches Cap’s line when the player enters interact range; opening the panel shows an **instant authored fallback**, then swaps in the cached Ollama line when ready. **Another line** re-rolls; **Ask Cap** accepts typed player questions with multi-turn memory. Ask Cap now uses `/api/chat` (system + user messages) while voice fetch remains `/api/generate`. Quest facts stay in C#.
2. **Environmental flavor** — `DungeonFlavorZone` → `DungeonFlavorNarrator` → HUD toast on safe/encounter tiles (cooldown ~42 s).
3. **Trap layout** — `DungeonTrapPlanner` at level load requests JSON (`format: "json"`) listing grid coordinates and types (`spike`, `ember`, `slime`). `DungeonLevelBuilder.IsTrapEligibleCell` validates every cell; `DungeonLootScatter.ScatterTraps` places AI traps first, then fills the remainder procedurally.
4. **Loot scatter** — `DungeonContentPlanner` JSON `loot` entries (`pebble` / `ration`) on validated walkable cells; procedural fill to Inspector quotas.
5. **Enemy placement** — same plan’s `enemies` array on **E** tiles only; procedural fill for `encounterEnemyCount`.
6. **Sign text** — same plan’s `signs` array places `DungeonSignPost` billboards on corridor cells with cosy-fantasy copy (4–12 words, sanitized in C#).

Both dialogue and flavor respect UI gates (`NarrationUiGate`) so prompts do not fire during pause or dialogue overlap.

Cap personality and voice/reactive prompts live in **`prompts/cap_personality.jinja2`**, rendered at runtime by **`prompts/render_cap_prompt.py`** (Python + Jinja2) via `CapPersonalityPromptBuilder`. This separates prompt iteration from Unity builds and mirrors the cosy-fantasy rules in one file. Earlier C#-embedded templates (which caused qwen3 to echo “Quest title:” blocks) are archived in `docs/prompts-used.md` §2.

Options menu exposes **`GameSettings.LlmEnabled`**; when false, NPC lines use canned text, flavor narration is skipped, and trap placement falls back to purely procedural scatter — supporting players who disable AI or lack hardware headroom.

## 4. Performance considerations

Playtest feedback emphasized that **no player will wait 15 seconds** for optional AI. The architecture front-loads work:

- **Main Menu warm-up** (`OllamaMenuWarmup` → `WarmupModelCoroutine`, 8-token completion) reduces cold-start latency before Level1.
- **Proximity prefetch** for NPC voice while walking toward Cap (`NpcDialogueCache`).
- **Level-load prefetch** for trap/content JSON while loot and enemies spawn immediately; trap and content planners run **sequentially** (`PrefetchAiPlansSequential`) to avoid aborting each other on the single-flight client; validated placement when plans arrive (timeout ~8 s) or procedural fill.
- **Non-streaming** NPC requests with tight `num_predict` (~80 via `defaultNpcMaxTokens`) reduce perceived stall versus click-to-stream UX.
- **Token caps** (~72 flavor, ~80 NPC, ~240 content JSON) bound worst-case latency.
- Work runs on **coroutines**; the main thread only updates UI and spawns validated entities.

**Known limit:** `OllamaHandler` remains single-flight — dialogue during level load can still cancel an in-flight planner request, but trap/content planners no longer run in parallel with each other. Superseded aborts (`HTTP 0`) are suppressed in logs. A full request queue is future work. Benchmarks belong in `docs/eval/` (TODO).

## 5. Gameplay impact

The LLM supports the **cosy fantasy** tone described in `docs/high-concept.md`. Static quest text stays readable for assessment; AI lines, signs, and trap variety reward players who install Ollama. Safe (**S**) and encounter (**E**) zones gain moment-to-moment mood; trap types (spike, ember, slime) change damage cadence without altering maze geometry.

Combat (`PlayerCombat`, `EnemyMeleeAI`) and loot are **not** LLM-driven — only placement flavor and sign copy come from the content planner. Two-way melee keeps progression authoritative in C# while AI adds ambience.

Trade-offs: small models occasionally break character or return invalid JSON; C# validation, `ExtractNpcSpokenDialogue`, and procedural fill preserve fairness. Cap prompts additionally require **Python + Jinja2** on the player machine. The static quest panel remains the source of truth for objectives.

## 6. Development workflow

AI-assisted tools accelerated implementation but did not replace design ownership:

| Tool | Role |
|---|---|
| **Cursor + Claude / GPT** | C# gameplay, UI fixes, prefetch/trap pipeline, documentation, NDJSON debug |
| **Ollama (runtime)** | Playtest dialogue, flavor, and trap JSON |
| **Meshy AI** | Cap and enemy meshes |
| **Pillow script** | Tileable dungeon albedos |

Every meaningful change is logged in `docs/refinements-changes.md` with date, rationale, and tool credit — satisfying auditability for academic submission. Prompt experiments are archived in `docs/prompts-used.md` and `Assets/DialogueOutput/ollama-dialogue.json`.

## 7. Playtest feedback and iteration

External playtesting (see [`feedback.summary.md`](./feedback.summary.md)) surprised us: reviewers commented on **gameplay clarity** (combat range, traps, text, lighting) but raised **no direct concerns about Cap dialogue or the LLM**. That outcome supports the non-blocking design — prefetch, authored fallbacks, and opt-out kept AI optional rather than a friction point.

Our main post-event engineering still targeted the **model pipeline**, even though feedback did not name it. Commits after the event moved Cap prompts to Jinja2-only rendering, strengthened qwen3 planning-text filtering, added main-menu warm-up, fixed Ask Cap so replies overwrite cleanly instead of stacking, and sequenced level-load trap/content planners to reduce Ollama abort noise.

Parallel **gameplay** passes addressed reviewer clarity concerns: melee hit detection and VFX, hazard emissive markers, maze lighting coverage, UI text readability (`TmpTextUtility`), and a **How to Play** panel on the main menu as lightweight onboarding. We deferred a full tutorial and animation overhaul as out of scope; rationale and feasibility notes are in [`critical.feedback.md`](./critical.feedback.md).

## 8. Conclusion

DungeonExporer demonstrates a **pragmatic local LLM integration**: Jinja2 Cap prompts, authored quest authority, validated trap/content cells, menu warm-up, health checks, and player opt-out. The architecture is intentionally simple (HTTP generate/chat + sanitize + JSON parse + external template render) so evaluators can trace data flow in a short technical video. Next steps: request-queue for concurrent Ollama calls, SimpleOllamaUnity consolidation, and expanded eval sets in `docs/eval/`.
