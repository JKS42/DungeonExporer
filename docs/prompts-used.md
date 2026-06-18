# Prompt Archive — DungeonExporer

> Record of prompts tested for Ollama integration.  
> Last updated: 2026-06-18  
> Raw log (debug): `Assets/DialogueOutput/ollama-dialogue.json`

---

## 1. Active prompts (in code)

### 1.0 Cap personality template (C# — runtime)

**File:** `Assets/Prompts/cap_personality.j2`  
**Context defaults:** `Assets/Prompts/cap_context.json`  
**Renderer:** `CharacterPersonalityTemplateManager.RenderCapPersonality` (DatingSim-style `{{ field }}` replacement).  
**Builder:** `CapPersonalityPromptBuilder` → voice mode (shipped UI).

Mirrors also under `Assets/StreamingAssets/Prompts/` for standalone builds.

**Legacy (offline only):** [`prompts/cap_personality.jinja2`](../prompts/cap_personality.jinja2) + [`prompts/render_cap_prompt.py`](../prompts/render_cap_prompt.py) — documentation and optional comparison; **not** called at runtime.

**Context variables:** `display_name`, `mode` (`voice` only in shipped UI), `quest_title`, `quest_briefing`, `quest_state`, `world_context`, `inventory_summary`, `memory_block`, `max_sentences`, `situation`, Cap role/background/mood fields from `cap_context.json`.

---

### 1.1 NPC voice line — Cap (`mode: voice`)

**Purpose:** In-character speech when the dialogue panel opens (prefetch) or the player clicks **Another line**. Quest facts remain **authored in C#** (`QuestDefinition`).

**Builder:** `CapPersonalityPromptBuilder.BuildVoicePrompt` → §1.0.

**Model / API:** Non-streaming `OllamaHandler.RequestGeneration` (`/api/generate`), `think: false`, `num_predict` from `GetEffectiveNpcMaxTokens()`. Output through `ExtractNpcSpokenDialogue` before UI / memory.

---

### 1.1b AI side quests (`AiQuestPlanner`)

**Purpose:** Two optional Cap errands at level load (`ai_cap_side_a`, `ai_cap_side_b`). Title, briefing, and HUD hints are LLM-written; **objective event ids** must match `QuestWorldEvents` (validated in C#).

**Builder:** `AiQuestPlanner.BuildPrompt()` — JSON schema with fixed quest ids and objective whitelist.

**Fallback:** Procedural side quests register immediately if Ollama is off or JSON fails.

**Model / API:** Non-streaming `RequestGeneration`, `jsonResponse: true`, `disableThinking: true`, queued after trap/content planners.

---

### 1.2 Dungeon flavor narrator (`DungeonFlavorNarrator`)

**Purpose:** One HUD toast when entering **S** (safe) or **E** (encounter) floor zones.

```
You are the dungeon narrator for a cosy fantasy first-person game.
The player just stepped onto {mint-tinted calm safe tiles | crimson encounter tiles that hum...}.
Write exactly one short sentence, second person ("you"), sensory mood only.
No instructions, no quest spoilers, no NPC names, no markdown. Max 26 words.
```

**Model / API:** Non-streaming `RequestGeneration`, `num_predict` ≤ 72, `disableThinking: true`, `ExtractFlavorLine`, cooldown ~42 s.

---

### 1.3 Level-load JSON planners

**Trap plan** (`DungeonTrapPlanner`): maze ASCII block + JSON schema for `spike` / `ember` / `slime` cells.  
**Content plan** (`DungeonContentPlanner`): `loot`, `enemies`, `signs` arrays.  
**Side quests** (`AiQuestPlanner`): fixed ids + `QuestWorldEvents` objective whitelist.  
**Model / API:** `RequestGeneration` with `jsonResponse: true`, validated in C# before spawn.

---

### 1.4 Debug / test UI (`OllamaHandler` + TMP fields)

Free-form prompts typed in the Level1 test panel. Used for connectivity checks, not shipped gameplay.

---

## 2. Superseded prompts (iterations)

### 2.3 Ask Cap reactive Q&A (removed 2026-06-18)

Player-typed **Ask Cap** used `CapPersonalityPromptBuilder.BuildReactivePrompt` + `OllamaHandler.RequestChat`. Removed from `DialoguePanelController`; Cap dialogue is one-way (voice + **Another line** only). `RequestChat` remains in `OllamaHandler` for potential future use.

### 2.4 Python Jinja2 Cap renderer (superseded 2026-06-18)

`CapPersonalityPromptBuilder` previously invoked `prompts/render_cap_prompt.py` (real Jinja2 subprocess). Replaced by `CharacterPersonalityTemplateManager` + `Assets/Prompts/cap_personality.j2` so Unity builds need no Python on the player machine.

### 2.5 C#-embedded Cap template (superseded 2026-06-11)

Hard-coded strings in `DialoguePanelController` replaced by external template files. See §1.0.

---

## 3. Failure modes observed

| Symptom | Model | Mitigation |
|---|---|---|
| Planning text in Cap voice / flavor | `qwen3:4b` | `ExtractNpcSpokenDialogue`, `ExtractFlavorLine`, `think: false`; enable **Fast AI responses** (`gemma3:4b`) |
| Empty Cap line after HTTP 0 | Any | Ollama FIFO queue; planners defer while dialogue open |
| Invalid trap / side-quest JSON | Any | C# validation + procedural / fallback fill |

See `Assets/DialogueOutput/ollama-dialogue.json` for dated examples.
