# Prototype & Final Build Notes

> For academic submission: what counts as prototype vs. final, and how to export.  
> Last updated: 2026-06-18

## Prototype (requirement met in repo)

The brief asks for a prototype demonstrating **at least one functional LLM-driven feature**.

**Delivered in repository (no separate binary required for prototype credit if assessors run Unity):**

| Feature | Evidence |
|---|---|
| LLM NPC dialogue | Level1 → Cap → auto voice + **Ask Cap** (Jinja2 prompt → Ollama) |
| LLM environment flavor | Enter **S** / **E** zones → HUD toast |
| Health check + setup panel | Start without Ollama → guided continue |

**Scene:** `Assets/Scenes/Level1.unity`  
**Entry:** Play in Editor or exported `.exe` with Ollama running.

Historical prototype issues (empty responses, planning text in UI) are documented in `docs/prompts-used.md` and fixed in current `OllamaHandler` / `DialoguePanelController`.

## Final build (you export)

The **final build** should be a **stable player executable** (or WebGL if allowed) that includes:

- Level1 gameplay loop (quests, two-way melee combat with hit feedback, save/load)
- **Main Menu** with **How to Play** and Ollama warm-up
- Python 3 + Jinja2 on the target machine (Cap prompts) or document graceful degrade
- Documented Ollama integration (or graceful degrade)
- No debug-only blockers (Console errors acceptable only if explained)

### Export steps (Windows standalone)

1. **File → Build Settings**
2. Scenes: `MainMenu` (optional), **`Level1`** (required)
3. Platform: **Windows** (or target per brief)
4. **Player Settings:** company/product name, default resolution 1920×1080
5. **Build** → e.g. `Builds/Windows/DungeonExporer.exe`
6. Test on a **clean machine** with:
   - `ollama pull qwen3:4b`
   - Ollama service running
   - `pip install jinja2` and Python on PATH
   - Wait on Main Menu for Ollama warm-up before Level1
7. Zip `DungeonExporer.exe` + `DungeonExporer_Data` folder

### What not to ship

- `Library/`, `Temp/`, `Logs/` (Unity cache)
- Unrotated API keys (`NeocortexSettings.asset` if still sensitive)
- Full `Assets/DialogueOutput/ollama-dialogue.json` if it contains personal test prompts (optional exclude)

### Version label

Tag submission build in README or release notes, e.g. **Final build — 2026-06-18 — Level1 slice**.

## Assessor quick test (no Unity)

1. Install Ollama + `qwen3:4b`
2. Run exported `.exe`
3. Main Menu → brief pause (warm-up) → Level1 → Cap → verify voice line + **Ask Cap**
4. Toggle LLM off in Options → verify canned line

## Related docs

- [`setup.md`](./setup.md) — install and controls  
- [`deliverables-checklist.md`](./deliverables-checklist.md) — full submission list  
- [`video-deliverables.md`](./video-deliverables.md) — recording guides  
