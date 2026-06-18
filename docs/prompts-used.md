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
**Builder:** `CapPersonalityPromptBuilder` → voice / reactive modes.

Mirrors also under `Assets/StreamingAssets/Prompts/` for standalone builds.

**Legacy (offline only):** [`prompts/cap_personality.jinja2`](../prompts/cap_personality.jinja2) + [`prompts/render_cap_prompt.py`](../prompts/render_cap_prompt.py) — documentation and optional comparison; **not** called at runtime.

**Context variables:** `display_name`, `mode` (`voice` | `reactive`), `quest_title`, `quest_briefing`, `quest_state`, `world_context`, `inventory_summary`, `memory_block`, `player_question` (reactive), `max_sentences`, `situation`, Cap role/background/mood fields from `cap_context.json`.

---

### 1.1 NPC voice line — Cap (`mode: voice`)

**Purpose:** In-character speech when the dialogue panel opens (prefetch) or the player clicks **Another line**. Quest facts remain **authored in C#** (`QuestDefinition`).

**Builder:** `CapPersonalityPromptBuilder.BuildVoicePrompt` → §1.0.

**Model / API:** Non-streaming `OllamaHandler.RequestGeneration` (`/api/generate`), `think: false`, `num_predict` from `GetEffectiveNpcMaxTokens()`. Output through `ExtractNpcSpokenDialogue` before UI / memory.

---

### 1.1b Ask Cap — reactive Q&A (`mode: reactive`)

**Purpose:** Player-typed questions in the dialogue panel (**Ask Cap**).

**Builder:** `CapPersonalityPromptBuilder.BuildReactivePrompt` → §1.0 with `player_question`.

**Memory:** `NpcConversationMemory` stores sanitized Cap turns + player questions (trimmed in fast mode).

**Model / API:** `OllamaHandler.RequestChat` (merges system + user into one `/api/generate` prompt, high-priority queue). Filtered line typewriter-revealed in UI. Console debug: `[Ask Cap] Player: "…" / Cap: "…"`.

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
**Model / API:** `RequestGeneration` with `jsonResponse: true`, validated in C# before spawn.

---

### 1.4 Debug / test UI (`OllamaHandler` + TMP fields)

Free-form prompts typed in the Level1 test panel. Used for connectivity checks, not shipped gameplay.

---

## 2. Superseded prompts (iterations)

### 2.1 Python Jinja2 Cap renderer (superseded 2026-06-18)

`CapPersonalityPromptBuilder` previously invoked `prompts/render_cap_prompt.py` (real Jinja2 subprocess). Replaced by `CharacterPersonalityTemplateManager` + `Assets/Prompts/cap_personality.j2` so Unity builds need no Python on the player machine.

### 2.2 C#-embedded Cap template (superseded 2026-06-11)

Hard-coded strings in `DialoguePanelController` replaced by external template files. See §1.0.

---

## 3. Failure modes observed

| Symptom | Model | Mitigation |
|---|---|---|
| Planning text in Ask Cap / flavor | `qwen3:4b` | `ExtractNpcSpokenDialogue`, `ExtractFlavorLine`, `think: false`; enable **Fast AI responses** (`gemma3:4b`) |
| Empty Cap line after HTTP 0 | Any | Ollama FIFO queue; planners defer while dialogue open |
| Player question echoed as Cap reply | `qwen3:4b` | Echo rejection in `ExtractNpcSpokenDialogue` |
| Invalid trap JSON | Any | `DungeonLevelBuilder.IsTrapEligibleCell` + procedural fill |

See `Assets/DialogueOutput/ollama-dialogue.json` for dated examples.
