# Project Deliverables Checklist

> Maps the brief (**Structure & Deliverables**) to files in this repo.  
> Last updated: 2026-06-18

Use this page when submitting the project. Items marked **you** must be produced outside the repo (videos, builds, name in credits).

---

## 1. Documentation

| Requirement | Status | Location |
|---|---|---|
| High concept (ideation, LLM role, why local) | Done | [`high-concept.md`](./high-concept.md) |
| Ollama plan (model, timing, data flow, prompts, risks) | Done | [`ollama-plan.md`](./ollama-plan.md) |
| Setup guide (install Ollama, run models, specs, **game requirements**) | Done | [`setup.md`](./setup.md) |
| Refinements / change log (scope + AI decisions) | Done | [`refinements-changes.md`](./refinements-changes.md) |
| README (overview, install, deps, credits, AI tools) | Done | [`../README.md`](../README.md) |
| Art direction (extra; asset prompts) | Done | [`art-direction.md`](./art-direction.md) |
| Prompt archive | Done | [`prompts-used.md`](./prompts-used.md) |
| LLM integration report (600–800 words) | Done | [`llm-integration-report.md`](./llm-integration-report.md) |
| Ethical considerations | Done | [`ethical-considerations.md`](./ethical-considerations.md) |
| Playtest feedback summary | Done | [`feedback.summary.md`](./feedback.summary.md) |
| Critical engagement with feedback | Done | [`critical.feedback.md`](./critical.feedback.md) |
| AI usage disclosure (annexure) | Done | [`ai-usage-annexure.md`](./ai-usage-annexure.md) |

---

## 2. Prototype & final build

| Requirement | Status | Notes |
|---|---|---|
| Prototype with ≥1 LLM feature | Done in repo | **Level1** — Cap voice, zone flavor, AI side quests, trap/content JSON plans |
| Final build (stable, refined) | **You** export | See [`build-notes.md`](./build-notes.md) |

**How to produce a build:** Unity → **File → Build Settings** → add `Level1` (and `MainMenu` if used) → **Build**. Test on a machine with Ollama + `qwen3:4b` pulled.

---

## 3. Two required videos (3–6 min each)

| Video | Status | Guide |
|---|---|---|
| Technical demonstration (local LLM + integration) | **You** record | [`video-deliverables.md`](./video-deliverables.md) §1 |
| Final showcase (gameplay, improvements, design intent) | **You** record | [`video-deliverables.md`](./video-deliverables.md) §2 |

Suggested capture: OBS / Xbox Game Bar; show Task Manager or `ollama list` briefly in the technical video.

---

## 4. Prompt archive

| Requirement | Status | Location |
|---|---|---|
| Record of tested prompts | Done | [`prompts-used.md`](./prompts-used.md) |
| Success + failure examples | Done | Same + `Assets/DialogueOutput/ollama-dialogue.json` |
| Iteration notes | Done | [`prompts-used.md`](./prompts-used.md) + [`refinements-changes.md`](./refinements-changes.md) |

---

## 5. LLM integration report

| Requirement | Status | Location |
|---|---|---|
| 600–800 words, technical + gameplay + workflow | Done | [`llm-integration-report.md`](./llm-integration-report.md) |

---

## 6. Ethical considerations

| Requirement | Status | Location |
|---|---|---|
| Transparency, licensing, crediting, player awareness | Done | [`ethical-considerations.md`](./ethical-considerations.md) |

---

## Quick pre-submission pass

- [ ] Skim **Main Menu → How to Play** in a fresh playthrough.
- [ ] Replace *your name* in [`README.md`](../README.md) Credits.
- [ ] Confirm `ollama pull qwen3:4b` on demo PC; match `GameSettings.LlmModel` / `OllamaHandler` default model.
- [ ] Optional: `ollama pull gemma3:4b` if demoing **Fast AI responses**.
- [ ] Playtest two-way combat (foe chase, hit VFX) + spike trap visibility + Cap voice + quest completion toast before recording showcase video.
- [ ] Record both videos using [`video-deliverables.md`](./video-deliverables.md).
- [ ] Export Windows (or target) build per [`build-notes.md`](./build-notes.md).
- [ ] Zip repo or submit Git link + build + videos per course instructions.
- [ ] Optional: rotate/remove any committed API keys (`NeocortexSettings.asset` — see ethical doc).
