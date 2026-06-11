# Prompt Archive — DungeonExporer

> Record of prompts tested for Ollama integration.  
> Last updated: 2026-06-11  
> Raw log (debug): `Assets/DialogueOutput/ollama-dialogue.json`

---

## 1. Active prompts (in code)

### 1.0 Cap personality template (Jinja2 — design source)

**File:** [`prompts/cap_personality.jinja2`](../prompts/cap_personality.jinja2)

Jinja2 template that defines Cap’s **personality bible** (macros) and assembles voice-line / Ask Cap prompts. **Runtime:** `CapPersonalityPromptBuilder` calls **real Jinja2** through [`prompts/render_cap_prompt.py`](../prompts/render_cap_prompt.py) (no C# template engine). Standalone builds mirror `prompts/` into `Assets/StreamingAssets/Prompts/`.

**Example context:** [`prompts/cap_example_context.json`](../prompts/cap_example_context.json)

```bash
pip install jinja2
python prompts/render_cap_prompt.py prompts/cap_personality.jinja2 prompts/cap_example_context.json
```

**Context variables:** `display_name`, `mode` (`voice` | `reactive`), `quest_title`, `quest_briefing`, `quest_state` (`considering` | `active` | `completed`), optional `world_context`, `inventory_summary`, `memory_block`, `player_question` (reactive), `max_sentences`, `situation` (override).

---

### 1.1 NPC voice line — Cap (`mode: voice`)

**Purpose:** In-character speech when the dialogue panel opens (prefetch) or the player clicks **Another line**. Quest title, briefing, and objectives remain **authored in C#** (`QuestDefinition`); the model only improvises banter.

**Builder:** `CapPersonalityPromptBuilder.BuildVoicePrompt` → Jinja2 §1.0.

**Context:** `display_name`, `quest_title`, `quest_briefing`, `quest_state` (`considering` | `active` | `completed`), `inventory_summary`, `memory_block`, optional `situation` override, `max_sentences`.

**Model / API:** Non-streaming `RequestGeneration`, `think: false`, `num_predict` ≈ 80 (`defaultNpcMaxTokens`). Output passed through `ExtractNpcSpokenDialogue` before UI / memory.

---

### 1.1b Ask Cap — reactive Q&A (`mode: reactive`)

**Purpose:** Player-typed questions in the dialogue panel (**Ask Cap**).

**Builder:** `CapPersonalityPromptBuilder.BuildReactivePrompt` → Jinja2 §1.0 with `player_question`.

**Memory:** `NpcConversationMemory` stores sanitized Cap turns + player questions for follow-ups.

**Model / API:** Same as §1.1 (non-streaming). Console debug: `[Ask Cap] Player: "…" / Cap: "…"`.

---

### 1.2 Dungeon flavor narrator (`DungeonFlavorNarrator`)

**Purpose:** One HUD toast when entering **S** (safe) or **E** (encounter) floor zones.

```
You are the dungeon narrator for a cosy fantasy first-person game.
The player just stepped onto {mint-tinted calm safe tiles | crimson encounter tiles that hum...}.
Write exactly one short sentence, second person ("you"), sensory mood only.
No instructions, no quest spoilers, no NPC names, no markdown. Max 26 words.
```

**Model / API:** Non-streaming `RequestGeneration`, `num_predict` ≤ 72, cooldown ~42 s.

---

### 1.3 Debug / test UI (`OllamaHandler` + TMP fields)

Free-form prompts typed in the Level1 test panel. Used for connectivity checks, not shipped gameplay.

---

## 2. Superseded prompts (iterations)

### 2.1 C#-embedded Cap template (superseded 2026-06-11)

Hard-coded strings in `DialoguePanelController` (and briefly `CapJinja2PromptRenderer`) replaced by **`prompts/cap_personality.jinja2`** + Python Jinja2 renderer. See §1.0.

---

### 2.2 Early Cap prompt (failed — empty or meta replies)

Used before streaming fixes and `Cap: "` suffix (see `ollama-dialogue.json` 2026-05-15 entries).

```
You are Cap, an NPC in a first-person dungeon crawler videogame.
Authoritative quest title: Cap's corridor drill.
Authoritative briefing (facts): Cap wants you to wallop the training dummy...
Quest state summary from the game: ...
Inventory: empty.
The adventurer is considering your quest. Speak in character...
Write 2–6 short sentences of spoken dialogue only. No markdown...
```

**Failure modes:**

- `"response": ""` — tokens went to `thinking` (qwen3 reasoning).
- Model restated instructions: *"We are Cap, an NPC in a first-person dungeon crawler game. The quest title is..."*

**Mitigations applied:**

1. `think: false` / `disableThinking` on gameplay requests.
2. Prompt ends with `Cap: "` so completion continues dialogue.
3. `OllamaHandler.ExtractNpcSpokenDialogue` strips planning lines.
4. `NpcConversationMemory` — avoid repetition (partial; polluted memory from bad replies required clearing during dev).

---

### 2.3 Planned chat-style template (`ollama-plan.md`)

Not yet wired to gameplay; target for `SimpleOllamaUnity` migration:

```
[SYSTEM] You are {npc.name}, a {npc.role} in a cosy, whimsical dungeon...
[CONTEXT] Room, inventory, recent events
[HISTORY] last 4 turns
[USER] player input
```

---

## 3. Successful examples

### Flavor (safe zone) — typical good output

**Prompt:** §1.2 with safe tiles.  
**Example response:** *"You breathe easier as the stone underfoot softens to a quiet, mossy hush."*  
**Why it worked:** Short constraint, no quest facts, single sentence.

### Test UI — generic chat

**Prompt:** `Hi, How are you doing`  
**Response:** Conversational greeting (not used in-game).  
**Why it worked:** No role-play constraints; model defaults to assistant mode (acceptable only in debug UI).

---

## 4. Failed examples (documented)

| Prompt type | Symptom | Cause | Fix |
|---|---|---|---|
| Cap dialogue (old template) | Empty UI | `response` empty, content in `thinking` | `think: false`, stream parser fallback |
| Cap dialogue | Planning text in LLM box | qwen3 meta-reasoning | `ExtractNpcSpokenDialogue`, `Cap: "` suffix |
| Cap + memory polluted | Repeated "We are Cap..." | Bad replies stored in memory | Sanitize before `AppendAssistantReply`; dev: clear PlayerPrefs / memory |
| Long briefing in prompt | Model lists "Quest title:" | Weak instruction following | Jinja2 personality + `ExtractNpcSpokenDialogue`; separate static quest UI block |
| Ask Cap greyed out | `_busy` not cleared after abort | Voice fetch interrupted | `ReleaseDialogueInputLock` + try/finally in coroutines |
| Missing Python/Jinja2 | Empty Cap prompt | Subprocess render failed | `pip install jinja2`; see `docs/setup.md` |

---

## 5. Iteration notes

1. **Split authority:** Quest UI shows fixed copy; LLM only flavors voice. Prevents quest-breaking hallucinations.
2. **Keep prompts short** for 4B models; flavor narrator capped at 26 words.
3. **Cosy tone** enforced in `high-concept.md` and situation strings — no horror/grimdark.
4. **Local only** — prompts never leave `localhost:11434`.
5. **Player opt-out** — `GameSettings.LlmEnabled` (Options); canned Cap line when off (see `ethical-considerations.md`).

---

## 6. How to add a new prompt

1. Implement in code under `Assets/Scripts/` or `prompts/*.jinja2` (prefer single builder / template).
2. Log a row in this file (template + purpose).
3. Add success/failure notes after playtesting.
4. If behavior changes scope or risks, update [`ollama-plan.md`](./ollama-plan.md) and [`refinements-changes.md`](./refinements-changes.md).
