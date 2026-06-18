# Annexure

## Disclosure of AI Usage in my Assessment:

### Section within the assessment in which generative AI was used:

- Ideation and scope framing (`docs/high-concept.md`).
- LLM implementation planning and risk design (`docs/ollama-plan.md`).
- Prompt engineering and iterations (`docs/prompts-used.md`, `prompts/cap_personality.jinja2`).
- Local LLM gameplay integration and testing (`Assets/Scripts/OllamaHandler.cs`, `DialoguePanelController.cs`, `CapPersonalityPromptBuilder.cs`).
- Gameplay refinements from feedback (combat/trap/UI/lighting/How to Play — see `docs/critical.feedback.md`, `docs/feedback.summary.md`, and `docs/refinements-changes.md`).
- Technical and reflective reporting (`docs/llm-integration-report.md`, `docs/critical.feedback.md`, `docs/feedback.summary.md`).

### Name of AI tool(s) used:

- Cursor AI assistant (Claude/GPT models via Cursor).
- Ollama local models (primarily `qwen3:4b`, with `gemma3:4b`/`llama3` as tested alternatives).
- Meshy AI (character asset generation for Cap/enemy visuals).

### Purpose/intention behind use:

- Brainstorm and iterate design decisions faster while keeping final decisions human-authored.
- Assist with code drafting/refactoring, debugging, and documentation maintenance.
- Generate in-game local LLM dialogue/flavor content at runtime through Ollama.
- Support art prototyping for characters and environment style exploration.
- Improve response quality and reliability through prompt/template iteration (Jinja2-based Cap prompts).

### Date(s) in which generative AI was used:

- Ongoing across the project timeline.
- Documented examples in repository logs: 2026-05-14 to 2026-06-18.
- Most recent documented AI-assisted updates: 2026-06-18 (`docs/refinements-changes.md` top entries — playtest UX, How to Play, compiler warning fixes).

### Evidence of AI usage:

- `docs/refinements-changes.md` (dated, append-only entries with an explicit "AI tool(s)" field).
- `docs/prompts-used.md` (prompt archive with tested prompts and outcomes).
- `docs/llm-integration-report.md` (toolchain and pipeline summary).
- `docs/critical.feedback.md` and `docs/feedback.summary.md` (AI-assisted analysis/write-up workflow).
- `prompts/cap_personality.jinja2` and `prompts/render_cap_prompt.py` (prompt/template artifacts).
- `Assets/DialogueOutput/ollama-dialogue.json` (runtime dialogue generation records).
- Git history entries referencing AI-assisted work (e.g., Jinja2 prompt migration, Ollama response updates, feedback docs updates).
