# Refinements & Changes Log — DungeonExporer

> Continuous, append-only log of scope changes and AI-assisted decisions.
> Newest entries at the top. Every entry follows the template below.

## Entry template

```
## YYYY-MM-DD — Short title
**Type**: scope-change | decision | refactor | dependency | risk
**AI tool(s)**: e.g. Cursor + Claude Opus 4.7

**What changed**: ...
**Why**: ...
**Impact / docs touched**: ...
**Follow-ups**: ...
```

---

## 2026-05-13 — Player character v0 (Meshy) + art-direction doc
**Type**: scope-change + dependency
**AI tool(s)**: Meshy AI (text-to-3D) + Cursor + Claude Opus 4.7 (organisation / docs)

**What changed**:
- The first 3D asset for the project — the player character ("The Adventurer") — was generated with **Meshy AI** using a ~750-character text-to-3D prompt (recorded verbatim in `docs/art-direction.md`).
- Files were dropped into `Assets/Meshy_AI_Stylized_full_body_3D_0513084955_texture_fbx/`. They have been moved + renamed to:
  - `Assets/Art/Characters/Adventurer/Adventurer.fbx`
  - `Assets/Art/Characters/Adventurer/Adventurer_BaseColor.png`
  - `Assets/Art/Characters/Adventurer/Adventurer_Metallic.png`
  - `Assets/Art/Characters/Adventurer/Adventurer_Normal.png`
  - `Assets/Art/Characters/Adventurer/Adventurer_Roughness.png`
- New top-level art folder convention established: `Assets/Art/<Category>/<AssetName>/`. Recorded in `AGENTS.md`.
- New documentation file `docs/art-direction.md` created as the single source of truth for the project's visual style, palette, and every asset's generation prompt + tool settings.
- `README.md` updated: project structure now shows `Art/`, credits + AI-tools table now lists Meshy.
- `AGENTS.md` updated: documentation-update rules now include a rule for new art assets.

**Why**:
The character is the first piece of generated art and needs a reproducible record. The naming the generator produced (`Meshy_AI_Stylized_full_body_3D_0513084955_texture_*`) leaked an implementation detail into Unity asset paths, which would have made scenes brittle and ugly. Clean names also let us re-roll the asset in the future without changing references.

**Impact / docs touched**:
- New: `docs/art-direction.md`, `Assets/Art/Characters/Adventurer/` folder.
- Edited: `README.md`, `AGENTS.md`, `docs/refinements-changes.md` (this entry).
- Removed: empty `Assets/Meshy_AI_Stylized_full_body_3D_0513084955_texture_fbx/` folder.

**Follow-ups**:
- First-time Unity step: open the `.fbx`, extract materials + textures into the same folder (see `docs/art-direction.md` § "First-time Unity import"). Because the textures were renamed, the FBX's embedded material won't auto-link; user wires it up once.
- Auto-rig the character with **Mixamo** (free) so we have walk/run/idle animations.
- Place the Adventurer prefab in `Level1.unity` once a player controller exists.
- Decide on a polish pass — do we keep this Meshy v0 as the final, or commission/regenerate a higher-quality hero asset?

---

## 2026-05-13 — Options panel layout fix
**Type**: refactor
**AI tool(s)**: Cursor + Claude Opus 4.7

**What changed**: Fixed an Options panel where the rows overflowed the panel and the footer (Reset / Back) appeared in the middle of the list instead of pinned to the bottom.

Root cause: the options Content `VerticalLayoutGroup` had `childControlHeight = false`, so it ignored each row's `LayoutElement.preferredHeight = 48` and used the default `RectTransform` height (~100 px). Seven rows × ~100 px overflowed the 520 px content area by ~270 px.

Fixes applied to `Assets/Scripts/UI/MainMenuController.cs`:
- Content VLG → `childControlHeight = true`.
- Each row's HLG → `childForceExpandHeight = false` so sliders/toggles stay at their preferred 28 / 36 px inside a 48 px row instead of being stretched.
- Panel resized 900 × 760 → 960 × 880 for breathing room.
- Toggle indicator: red "X" → green "✓" (uses Unity's built-in Toggle.graphic show/hide).
- Resolution dropdown LayoutElement now sets `preferredHeight` and `flexibleWidth` so it fills the row consistently.
- Toggle host now spans the full remaining row width (bigger click target) with the box right-anchored.

**Why**: The previous build was visually broken (rows spilling past the panel; AI-dialogue toggle hanging below the parchment area).

**Impact / docs touched**: `Assets/Scripts/UI/MainMenuController.cs` only.

**Follow-ups**:
- Add a `ScrollRect` to the options content if we ever exceed ~8 rows.
- Replace the procedural slider/toggle visuals with hand-painted parchment-style art when we have it.

---

## 2026-05-13 — Main menu, settings, and tone lock-in
**Type**: scope-change + decision
**AI tool(s)**: Cursor + Claude Opus 4.7

**What changed**:
- **Tone locked in**: *lighthearted fantasy* (warm parchment, sun-gold, mossy green). Recorded in `high-concept.md` and codified in `Assets/Scripts/UI/MenuTheme.cs`. LLM system prompts will be constrained to this tone in `ollama-plan.md` going forward.
- Added a **Main Menu** that is built procedurally at runtime — no editor wiring needed:
  - `Assets/Scripts/UI/MainMenuController.cs` — title, "Start Adventure", "Options", "Quit", plus an Options panel.
  - `Assets/Scripts/UI/MenuTheme.cs` — single source of truth for menu palette + typography.
- Added a **settings system** with PlayerPrefs persistence:
  - `Assets/Scripts/Settings/GameSettings.cs` — typed get/set + `OnChanged` event.
  - `Assets/Scripts/Settings/SettingsApplier.cs` — applies settings to `AudioListener` and `Screen` on load and on change; `DontDestroyOnLoad`.
- **Adjustable settings (v1)**: Master / Music / SFX volume, mouse sensitivity, fullscreen, resolution, *AI-driven dialogue (Ollama) toggle*.
  - Music/SFX bus separation is recorded as a follow-up (needs an `AudioMixer`).
- `Assets/Scenes/MainMenu.unity` was updated: the old "Ollama" test GameObject is now the "MainMenu" host for `MainMenuController`. The previous `Test.cs` script reference was removed from the scene (the file itself is left in `Assets/SimpleOllamaInjection/.../Examples/` until we delete the duplicate Ollama clients).

**Why**:
A real main menu unblocks playtesting the full game-loop scaffolding (Menu → Level → back to Menu). Locking the tone now means every later asset (UI, NPCs, prompts) can be authored against a single style.

**Impact / docs touched**:
- `docs/high-concept.md` — tone section + pitch updated.
- `docs/refinements-changes.md` — this entry.
- `Assets/Scenes/MainMenu.unity` — script attachment swapped.
- `Assets/Scripts/UI/` and `Assets/Scripts/Settings/` — new folders.

**Follow-ups**:
- Add `MainMenu` and `Level1` scenes to **Build Settings → Scene List** (the Start button checks `Application.CanStreamedLevelBeLoaded` and logs a clear error if missing).
- Pin `LlmEnabled` into the Ollama call sites — when `false`, dialogue systems must fall back to canned text.
- Replace the procedural menu with editor-built UI + a custom fantasy font once we have one.
- Wire Music / SFX volume to an `AudioMixer` (currently only Master is applied).

---

## 2026-05-13 — Documentation scaffolding & repo conventions
**Type**: decision
**AI tool(s)**: Cursor + Claude Opus 4.7

**What changed**:
- Created `AGENTS.md` at the repo root with rules for AI agents working in this project.
- Created `docs/` folder containing:
  - `high-concept.md`
  - `ollama-plan.md`
  - `setup.md`
  - `refinements-changes.md` (this file)
- Expanded the root `README.md` from a 1-line placeholder to a real overview.

**Why**:
The project will be assessed (in part) on documentation quality. Establishing the five required documents up front makes it easy to keep them current as the project evolves, instead of trying to reconstruct them at the end.

**Impact / docs touched**: All five docs created. `AGENTS.md` defines how they should be maintained.

**Follow-ups**:
- Fill in the *Inspirations & references* section of `high-concept.md`.
- Replace target inference times in `ollama-plan.md` with measured numbers once a benchmark scene exists.
- Decide on tone (dark fantasy vs. lighthearted) — affects system prompts.

---

## 2026-05-13 — Initial observation of repository state
**Type**: decision
**AI tool(s)**: Cursor + Claude Opus 4.7

**What changed**:
No code changed; this is an observation entry to record the starting point.

**Observed state**:
- Unity `6000.3.8f1` project with URP, Input System, and TextMesh Pro.
- **Three** independent ways to talk to Ollama already exist:
  1. `Assets/Scripts/OllamaHandler.cs` — UI-driven `UnityWebRequest` tester.
  2. `Assets/Ollama/OllamaRequester.cs` — minimal Ollama example using `Newtonsoft.Json`, hardcoded to `llama3`.
  3. `Assets/SimpleOllamaInjection/SimpleOllamaUnity/Ollama.cs` — `Microsoft.Extensions.AI` based wrapper with chat history and `<think>` stripping.
- A Neocortex SDK reference (`Assets/Resources/Neocortex/NeocortexSettings.asset`) with a plaintext `apiKey` is committed to git.
- Two scenes exist: `MainMenu.unity` (default lights/camera + a `Test.cs` Ollama example) and `Level1.unity` (UI scene that tests Ollama via `OllamaHandler` + `PromptTester`).
- No gameplay code yet (no player controller, dungeon generator, enemies, inventory, etc.).

**Decisions taken**:
- The game will be **3D**.
- We will standardise on `HardCodeDev.SimpleOllamaUnity.Ollama` as the single Ollama client going forward. The other two will be removed or wrapped once dependent scenes are updated.
- Default model: `qwen3:4b` (matches what's already wired in).

**Follow-ups**:
- Rotate and gitignore the Neocortex API key — and decide whether Neocortex stays in the project at all.
- Build a minimal player + camera + first 3D room to validate the pipeline.
- Define `RoomDefinition` / `NpcDefinition` ScriptableObjects.

---
