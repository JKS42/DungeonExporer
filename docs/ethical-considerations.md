# Ethical Considerations — DungeonExporer

> Transparency, licensing, crediting, and player awareness for AI use in this project.  
> Last updated: 2026-05-15

## 1. Transparency

### 1.1 What uses AI

| Feature | AI? | Player-visible? |
|---|---|---|
| Quest titles, objectives, rewards | **No** — authored in `QuestManager` | Yes (HUD / dialogue panel) |
| NPC “Hear them out” lines | **Yes** — Ollama | Labelled in UI: *“Hear them out (Ollama, streams)”* |
| Safe / encounter floor toasts | **Yes** — Ollama | Short HUD flavor line; no label (ambient) |
| Level layout, combat, pickups | **No** | — |
| 3D characters (Cap, foes) | **Generated asset** (Meshy) | Visual only; not runtime LLM |
| Wall / floor textures | **Procedural** (Pillow script) | Not generative AI at runtime |

### 1.2 What leaves the device

- **Runtime LLM:** HTTP to `localhost:11434` only. No cloud inference in the shipping integration path.
- **Development:** Using Cursor or cloud assistants during coding is separate from the player runtime; see README credits.

### 1.3 Documentation for assessors

- Design intent: [`high-concept.md`](./high-concept.md) §2–3  
- Technical flow: [`ollama-plan.md`](./ollama-plan.md), [`llm-integration-report.md`](./llm-integration-report.md)  
- Prompt history: [`prompts-used.md`](./prompts-used.md)

## 2. Player awareness & control

1. **First run:** If Ollama or the model is missing, a setup panel explains the requirement and links to [`setup.md`](./setup.md). **Continue** allows play without AI dialogue.
2. **Options:** **LLM / AI dialogue** toggle (`GameSettings.LlmEnabled`). When off:
   - **Hear them out** shows a short canned Cap line (no network call).
   - Zone flavor narration is skipped.
3. **Quest clarity:** Objectives and acceptance do not depend on model output; players are not penalized for disabling AI.

**Recommendation for showcase video:** Mention that dialogue is optional and local.

## 3. Licensing

| Component | License / terms | Action |
|---|---|---|
| Unity Engine | Unity Terms | Use Editor version in `ProjectVersion.txt` |
| Ollama | [Ollama license](https://github.com/ollama/ollama/blob/main/LICENSE) | Player installs separately |
| Model weights (`qwen3:4b`, etc.) | Per-model license on ollama.com | Document model name in submission |
| SimpleOllamaUnity | Check upstream repo LICENSE | Credit in README |
| TextMesh Pro | Unity package license | Included with Unity |
| Meshy-generated FBX | [Meshy terms](https://www.meshy.ai/) | Verify commercial/educational use for your submission |
| Procedural textures | Project-authored (Pillow) | No third-party texture license |

**Project license:** README states *To be decided* — set before public release (e.g. MIT for code, separate notice for art).

## 4. Crediting

### 4.1 In README

[`README.md`](../README.md) lists:

- Human author (placeholder — **replace with your name** before submission)
- Ollama, Unity, Meshy, Cursor/Claude
- Link to [`refinements-changes.md`](./refinements-changes.md) for AI-assisted development log

### 4.2 In-game

Consider adding a **Credits** section on the main menu (future) listing Ollama and Meshy. Currently crediting is documentation-first for the academic brief.

### 4.3 Academic integrity

- **`refinements-changes.md`** records AI-assisted decisions with tool names and dates.
- **`prompts-used.md`** archives prompts; do not present model-generated prose as sole authored design without attribution.
- Code written with Cursor should still be understood and demonstrable in the technical video.

## 5. Content safety

- **System tone:** Cosy fantasy; prompts discourage horror, profanity, and grimdark (`high-concept.md`).
- **Output filtering:** Sanitization and dialogue extraction reduce instruction leakage; not a full moderation pipeline.
- **Risk:** Small local models can still produce off-tone lines. Mitigation: short caps, authored quest facts, player can disable LLM.
- **No user-generated prompts** in shipped gameplay (debug panel only in dev scenes).

## 6. Secrets & data hygiene

- **`NeocortexSettings.asset`** has contained a plaintext API key in the past — **rotate**, **gitignore**, and remove from submission builds if unused.
- **`Assets/DialogueOutput/ollama-dialogue.json`** may contain playtest prompts — review before publishing; exclude from build if sensitive.

## 7. Fairness & assessment

- Markers can complete all quests with LLM disabled.
- LLM features are demonstrative, not a hidden difficulty multiplier.
- Technical video should show Ollama running locally to prove integration requirements.

## 8. References

- [`deliverables-checklist.md`](./deliverables-checklist.md)  
- [`ollama-plan.md`](./ollama-plan.md) §5 Risks  
- IIE brief: ethical considerations (transparency, licensing, crediting, player awareness)
