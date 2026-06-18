# LLM Integration Report — DungeonExporer

> Word count: ~800 (body).  
> Last updated: 2026-06-18

## 1. Introduction

*DungeonExporer* is a first-person dungeon slice built in Unity 6000.3.8f1. Its distinguishing feature is optional narrative and layout flavor from a **local** large language model through [Ollama](https://ollama.com), bound to `http://localhost:11434`. The game remains fully playable when Ollama is missing: quests, combat, traps, and UI use authored C# content. The LLM adds atmosphere, NPC voice, and trap placement suggestions — not core progression logic.

This report summarizes technical decisions, integration strategy, performance considerations, playtest-driven iteration, and how AI tools shaped the development workflow. Critical engagement with reviewer feedback is documented separately in [`critical.feedback.md`](./critical.feedback.md); raw reviewer notes are in [`feedback.summary.md`](./feedback.summary.md).

## 2. Technical decisions

### 2.1 Why Ollama and a local model

We chose Ollama over cloud APIs for four reasons aligned with the project brief: **no per-token cost** during iteration and playtesting; **offline play** after the model is pulled; **privacy** (prompts stay on the player machine); and **pedagogical clarity** for demonstrating integration in a technical video. **Normal mode** defaults to **`qwen3:4b`**; **Fast AI responses** (Options) switches to **`gemma3:4b`** with shorter prompts and lower token caps. **`llama3`** remains an optional quality tier. Planning-text leakage from reasoning models is mitigated in post-processing and by fast mode.

### 2.2 Client architecture

Gameplay uses `OllamaHandler` (`Assets/Scripts/OllamaHandler.cs`) with UnityWebRequest. Shipped paths call **`/api/generate`** (non-streaming for voice, flavor, planners, and AI side quests). A FIFO **request queue** serializes calls. Optional NDJSON streaming remains on the Level1 test UI only. **SimpleOllamaUnity** is included for future consolidation but is not the primary path.

Reasoning models may leak planning text; we send **`think: false`** where supported and sanitize outputs (`SanitizeModelOutput`, `ExtractNpcSpokenDialogue`, `ExtractFlavorLine`). Dialogue close aborts in-flight voice via generation guards.

### 2.3 Authoritative game state vs. stateless LLM

The LLM is **stateless between calls**. Each request rebuilds context from:

- `QuestDefinition` (title, briefing — not generated),
- `QuestManager.BuildPromptContext()` (active/completed quests),
- `PlayerInventory.BuildSummaryForPrompt()`,
- `NpcConversationMemory` (recent turns per NPC id),
- `DungeonLevelBuilder.BuildMazePromptBlock()` (ASCII maze for trap planning).

Quest acceptance, objectives, trap **validation**, and rewards are **never** parsed blindly from model output.

### 2.4 Boot-time health check

`OllamaFirstRunHealthCheck` queries `/api/tags` and verifies the configured model name exists. Failure opens `OllamaSetupPanelController`, linking to `docs/setup.md`, with **Continue** so players without Ollama can still play.

## 3. Integration strategy

Six LLM touchpoints ship in Level1:

1. **NPC dialogue** — Prefetch while approaching Cap; panel shows **instant authored fallback**, then Ollama voice when ready. **Another line** re-rolls. Cap prompts from `Assets/Prompts/cap_personality.j2` via `CharacterPersonalityTemplateManager` (no Python at runtime). **No player-typed chat** in the shipped UI.
2. **Environmental flavor** — `DungeonFlavorZone` → `DungeonFlavorNarrator` → `ExtractFlavorLine` → HUD toast on safe/encounter tiles.
3. **AI side quests** — `AiQuestPlanner` JSON at level load; objectives validated against `QuestWorldEvents`; Cap offers via `QuestManager` dynamic registration.
4. **Trap layout** — `DungeonTrapPlanner` JSON at level load; cells validated in C#; procedural fill on timeout.
5. **Loot / enemies / signs** — `DungeonContentPlanner` JSON; procedural fill.
6. **Quest completion feedback** — C# only: `QuestManager.QuestCompleted` → HUD toast (not LLM-generated).

Planners wait **4 seconds** after spawn and **until the dialogue panel closes** so Cap voice wins at level start. Dialogue and flavor respect `NarrationUiGate`.

Options expose **`GameSettings.LlmEnabled`** and **`GameSettings.LlmFastMode`**. When LLM is off, Cap shows a canned line, **Another line** hides, and Ollama planners skip (fallback side quests still register).

## 4. Performance considerations

- **Main Menu warm-up** (`OllamaMenuWarmup`) — 8-token completion; re-warms on Options changes.
- **Proximity prefetch** for NPC voice (`NpcDialogueCache`).
- **Request queue** — prevents HTTP 0 abort storms between voice and planners.
- **Fast mode** — `gemma3:4b`, lower caps, 2-sentence Cap prompts.
- **Token caps** bound worst-case latency; work on coroutines.

Benchmarks belong in `docs/eval/` (TODO).

## 5. Gameplay impact

The LLM supports the **cosy fantasy** tone in `docs/high-concept.md`. Combat and loot are **not** LLM-driven. Playtest-driven readability passes (black gameplay text, trap emissive markers, lighting coverage, How to Play panel) addressed reviewer clarity concerns without making the LLM a gatekeeper.

Cap prompts require **no Python** on the player machine — only Ollama and a pulled model.

## 6. Development workflow

| Tool | Role |
|---|---|
| **Cursor + Claude / GPT** | C# gameplay, UI, Ollama queue, documentation |
| **Ollama (runtime)** | Dialogue, flavor, trap/content JSON |
| **Meshy AI** | Cap and enemy meshes |
| **Pillow script** | Tileable dungeon albedos |

Changes are logged in `docs/refinements-changes.md`. Prompt experiments in `docs/prompts-used.md` and `Assets/DialogueOutput/ollama-dialogue.json`.

## 7. Playtest feedback and iteration

External playtesting (see [`feedback.summary.md`](./feedback.summary.md)) focused on **gameplay clarity** (combat, traps, text, lighting), not the LLM directly — supporting our non-blocking design.

Post-playtest work included: C# Cap template (DatingSim port), planning-text filters, Ollama request queue, fast mode, main-menu warm-up, **AI side quests** (`AiQuestPlanner`), removal of player-typed Ask Cap, **quest completion HUD**, and gameplay readability (`TmpTextUtility`, How to Play). Details in [`critical.feedback.md`](./critical.feedback.md).

## 8. Conclusion

DungeonExporer demonstrates **pragmatic local LLM integration**: C# Cap templates, authored quest authority, validated trap/content cells, queued HTTP, menu warm-up, health checks, fast mode, and player opt-out. Evaluators can trace data flow in a short technical video via `OllamaHandler`, `CharacterPersonalityTemplateManager`, and `docs/ollama-plan.md`. Next steps: SimpleOllamaUnity consolidation and eval sets in `docs/eval/`.
