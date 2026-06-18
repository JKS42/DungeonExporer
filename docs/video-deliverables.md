# Video Deliverables — DungeonExporer

> Shot lists for the two required 3–6 minute submission videos.  
> Last updated: 2026-06-18

Record with OBS, Xbox Game Bar, or Unity Recorder. Capture at **1920×1080** if possible. Show the game window full-screen; avoid desktop clutter.

**Before recording:** Ollama running, `ollama pull qwen3:4b` (and `gemma3:4b` if demoing fast mode). Wait on **Main Menu** for warm-up. Optionally open **How to Play**.

---

## 1. Technical demonstration (local LLM + integration)

**Goal:** Prove the local LLM pipeline is real, traceable, and grounded in authored game state.

**Suggested structure (3–6 min):**

| Segment | What to show | Talking points |
|---|---|---|
| Intro (30 s) | Title card or Main Menu | Cosy dungeon slice; Ollama on `localhost:11434` only |
| Setup (45 s) | Terminal: `ollama list`, optional `curl localhost:11434/api/tags` | Model tag matches `GameSettings.LlmModel` |
| Warm-up (30 s) | Main Menu → brief pause → Level1 | `OllamaMenuWarmup` reduces cold-start latency |
| Cap dialogue (90 s) | Walk to Cap → **E** → voice line → **Ask Cap** typed question | C# template `Assets/Prompts/cap_personality.j2`; quest facts in static UI block |
| Flavor (30 s) | Step on **S** / **E** floor tint | `DungeonFlavorNarrator` HUD toast |
| Level-load AI (60 s) | Restart level or show Console once | Trap/content JSON planners; C# validates cells; procedural fill on timeout |
| Opt-out (30 s) | Options → disable **AI-driven dialogue** → Cap again | Canned line; Ask Cap hidden |
| Code trace (45 s) | Editor: `OllamaHandler`, `CharacterPersonalityTemplateManager`, `Assets/Prompts/` | Request queue, sanitize output, facts in `QuestManager` |
| Close (15 s) | Point to `docs/ollama-plan.md`, `docs/prompts-used.md` | Local, optional, documented |

**Do show:** Task Manager or `ollama ps` briefly to prove local inference.

**Avoid:** Long silence waiting for first token — use Main Menu warm-up or pre-run `ollama run qwen3:4b` once.

---

## 2. Final showcase (gameplay, improvements, design intent)

**Goal:** Show the Level1 slice as a playable game: exploration, combat, quests, cosy tone.

**Suggested structure (3–6 min):**

| Segment | What to show | Talking points |
|---|---|---|
| Hook (30 s) | Main Menu **How to Play** (brief) → spawn in maze, torch lighting | First-person cosy dungeon; hybrid ASCII layout; controls in-game |
| Meet Cap (60 s) | Safe **S** room, quest accept, optional Ask Cap | LLM adds voice; objectives are reliable C# quests |
| Combat (90 s) | **E** pit: foe chases → left-click attack (swing + impact VFX, crosshair pulse) → kill | Two-way melee; not LLM-driven |
| Quest loop (60 s) | Return to Cap → complete drill → **Echoes in the dark** | Save **F5** / load **F9** optional |
| Hazards & loot (45 s) | Spike jump (striped + emissive marker), bubble pickup, corridor sign | Trap variety from Ollama plan + procedural fill |
| Tone (30 s) | Flavor toast or sign text | Lighthearted fantasy palette (`MenuTheme`) |
| Design intent (45 s) | Slide or in-game pause: high-concept bullets | Local LLM for flavor; authority in C# |
| Close (15 s) | Main menu or fade | Where to find repo + build notes |

**Emphasize:** AI is optional ambience; disabling it still yields a complete loop.

---

## Checklist before upload

- [ ] Both videos 3–6 minutes each
- [ ] Audio explains *what* and *why* (not only silent gameplay)
- [ ] Cap Ask Cap works (Ollama running; `Assets/Prompts/cap_personality.j2` present)
- [ ] No unrotated API keys visible in Editor captures
- [ ] File names match course submission instructions

## Related docs

- [`deliverables-checklist.md`](./deliverables-checklist.md) — full submission mapping
- [`build-notes.md`](./build-notes.md) — export standalone build for assessors
- [`ethical-considerations.md`](./ethical-considerations.md) — mention AI opt-out in showcase
