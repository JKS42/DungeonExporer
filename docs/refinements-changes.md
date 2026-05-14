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

## 2026-05-14 — Simple pause menu (Level1)
**Type**: scope-change
**AI tool(s)**: Cursor + GPT-5.3 Codex

**What changed**:
- Added `DungeonExporer.UI.PauseMenuController` (`Assets/Scripts/UI/PauseMenuController.cs`): **Escape** toggles pause, sets `Time.timeScale` to 0, shows a dim overlay + parchment panel with **Resume**, **Main Menu** (`MainMenu` scene), and **Quit**; styling follows `MenuTheme`.
- `FirstPersonController` skips movement / look / cursor handling while `PauseMenuController.IsPaused` is true so Escape is not double-consumed.
- `Level1.unity`: root **PauseMenu** object with the controller (`DefaultExecutionOrder` -10 so pause toggles before the player script).

**Why**:
Basic quality-of-life for a first-person level: stop play, navigate menus, or exit without killing the editor.

**Impact / docs touched**:
- New: `Assets/Scripts/UI/PauseMenuController.cs` + `.meta`.
- Edited: `Assets/Scripts/Player/FirstPersonController.cs`, `Assets/Scenes/Level1.unity`, `docs/refinements-changes.md` (this entry).

**Follow-ups**:
- If Escape should only unlock the mouse while playing (without pausing), move pause to **P** or **Tab** and restore the old Escape behaviour.

---

## 2026-05-14 — Dungeon wall brick texture (URP)
**Type**: scope-change + art
**AI tool(s)**: Cursor + GPT-5.3 Codex + Python (Pillow)

**What changed**:
- Added `Assets/Art/Environment/DungeonBrick/DungeonBrick_Albedo.png` (tileable procedural brick) and `DungeonBrickWall.mat` (URP Lit, base map only).
- `DungeonLevelBuilder` accepts an optional **`_wallMaterial`**; when set, wall cubes use that material with a **`MaterialPropertyBlock`** so `_BaseMap_ST` tiling matches **`_cellSize`**, **`_wallHeight`**, and **`_brickWorldMeters`** (~brick width in world units). If the material is unset, the old flat procedural wall color remains.
- `Level1.unity`: **Dungeon** references `DungeonBrickWall`.

**Why**:
Walls read as a 3D dungeon instead of flat grey blocks.

**Impact / docs touched**:
- New: `Assets/Art/Environment/` folder metas, `DungeonBrick/` textures + material.
- Edited: `Assets/Scripts/Dungeon/DungeonLevelBuilder.cs`, `Assets/Scenes/Level1.unity`, `docs/art-direction.md`, `docs/refinements-changes.md` (this entry).

**Follow-ups**:
- Optional normal + roughness maps for stronger depth under torch / point lights.
- Bake lightmaps or add subtle ambient occlusion once the dungeon mesh set stabilises.

---

## 2026-05-14 — Maze layout moved to TextAsset
**Type**: refactor + decision
**AI tool(s)**: Cursor + GPT-5.3 Codex

**What changed**:
- Dungeon ASCII is now edited in `Assets/Data/Dungeon/Level1_Maze.txt` (comment lines with `;`, blank lines ignored). `DungeonLevelBuilder` takes a **TextAsset** reference and parses at runtime; validation aborts the build if row widths differ, characters are invalid, or there is not exactly one `P` spawn.
- `Level1.unity`: **Dungeon** component wired to that TextAsset.

**Why**:
Designers can iterate maze layout without recompiling C#.

**Impact / docs touched**:
- New: `Assets/Data/Dungeon/Level1_Maze.txt`, folder metas under `Assets/Data/`.
- Edited: `Assets/Scripts/Dungeon/DungeonLevelBuilder.cs`, `Assets/Scenes/Level1.unity`, `README.md` (structure + status), `docs/refinements-changes.md` (this entry).

**Follow-ups**:
- Add more levels as additional `.txt` files + scene references, or load by name from `Resources/` when you have a level flow.

---

## 2026-05-14 — Prototype dungeon maze (floors, walls, safe vs encounter zones)
**Type**: scope-change
**AI tool(s)**: Cursor + GPT-5.3 Codex

**What changed**:
- Added `DungeonExporer.Dungeon.DungeonLevelBuilder`, which builds a **31×17 ASCII maze** at runtime into `Floors`, `Walls`, and `EncounterVolumes` children (URP Lit materials: neutral corridors, moss-tint **safe rooms** `S`, terracotta **encounter areas** `E`, stone **walls** `#`, spawn `P`).
- Each `E` cell gets a **trigger** volume plus `DungeonEncounterVolume` and a **kinematic Rigidbody** so future gameplay (and `OnTriggerEnter`) works with `CharacterController` movers.
- `Level1.unity`: added root **`Dungeon`** wired to the **Player** transform so spawn snaps to `P` on play (the scene may also include a separate **Ground** plane and **Adventurer** prefab from other changes).

**Why**:
Establishes a walkable, maze-like layout with clear **rest / safe** vs **danger** regions before combat or LLM encounter hooks exist.

**Impact / docs touched**:
- New: `Assets/Scripts/Dungeon/DungeonLevelBuilder.cs`, `DungeonEncounterVolume.cs`, and folder `.meta` files.
- Edited: `Assets/Scenes/Level1.unity`, `docs/high-concept.md` (core loop / world note), `docs/refinements-changes.md` (this entry).

**Follow-ups**:
- Replace primitive cubes with modular mesh tiles or probuilder art when art direction lands.
- Drive encounter / safe room metadata from a `ScriptableObject` or JSON instead of hard-coded strings when levels multiply.
- Subscribe to `DungeonEncounterVolume` triggers from a future `EncounterDirector` or combat system.

---

## 2026-05-14 — First-person character controller
**Type**: scope-change
**AI tool(s)**: Cursor + GPT-5.3 Codex

**What changed**:
- Added `DungeonExporer.Player.FirstPersonController` on `Assets/Scripts/Player/FirstPersonController.cs` — `CharacterController` movement (WASD / gamepad move), mouse and stick look, jump, sprint, crouch, gravity, and cursor lock (Escape unlocks, click to lock again).
- Wired to the existing `InputSystem_Actions` asset (`Player` action map: Move, Look, Jump, Sprint, Crouch).
- Updated `Assets/Scenes/Level1.unity`: `Player` root with `CharacterController` + `FirstPersonController`; **Main Camera** parented under the player at eye height; works with **Dungeon** geometry and any separate scene **Ground** used for playtesting.

**Why**:
The game needs a playable exploration loop before dungeon content; first-person was chosen as the default presentation.

**Impact / docs touched**:
- New: `Assets/Scripts/Player/FirstPersonController.cs`, `Assets/Scripts/Player.meta`, `Assets/Scripts/Player/FirstPersonController.cs.meta`.
- Edited: `Assets/Scenes/Level1.unity`, `docs/high-concept.md` (perspective locked to first-person), `docs/refinements-changes.md` (this entry).

**Follow-ups**:
- Replace or disable redundant **Ground** if dungeon floors fully cover walkable space (avoid z-fighting).
- Optional: parent a visible Adventurer mesh for body presence without breaking first-person (an **Adventurer** prefab instance may already exist in the scene for reference).
- Apply the same player prefab pattern to other gameplay scenes when they are added.

---

## 2026-05-13 — Dialogue JSON saved within Assets
**Type**: refactor + decision
**AI tool(s)**: Cursor + Claude Opus 4.7

**What changed**:
- `Assets/Scripts/OllamaHandler.cs` now saves the dialogue history to `Assets/DialogueOutput/ollama-dialogue.json` instead of `Application.persistentDataPath/ollama-dialogue.json`.
- The output directory is created automatically if it does not exist.

**Why**:
Saving within the Assets folder makes it easy to inspect, version control (or gitignore), and include in build outputs without relying on platform-specific user folders or the project root.

**Impact / docs touched**:
- Edited: `Assets/Scripts/OllamaHandler.cs`
- Edited: `docs/ollama-plan.md`
- Edited: `docs/refinements-changes.md` (this entry)

**Follow-ups**:
- Add `Assets/DialogueOutput/` to `.gitignore` if dialogue history should not be committed.

---

## 2026-05-13 — Ollama timeout widened for cold starts
**Type**: refactor + risk
**AI tool(s)**: Cursor + Claude Opus 4.7

**What changed**:
- `Assets/Scripts/OllamaHandler.cs` now uses a configurable request timeout (`requestTimeoutSeconds`) instead of a hard-coded 30 seconds.
- The default timeout was raised to 120 seconds so the local model has room to cold-load and answer without Unity aborting the request too early.

**Why**:
The Ollama model can take longer than 30 seconds to load or generate on first use, especially on slower hardware. The earlier hard cap was causing avoidable timeouts even when the endpoint was valid.

**Impact / docs touched**:
- Edited: `Assets/Scripts/OllamaHandler.cs`
- Edited: `docs/refinements-changes.md` (this entry)

**Follow-ups**:
- If the editor still times out after this, check whether the model is actually running in Ollama or whether the prompt is large enough to justify a separate warm-up call.

## 2026-05-13 — Dialogue JSON now appends history
**Type**: refactor + decision
**AI tool(s)**: Cursor + Claude Opus 4.7

**What changed**:
- `Assets/Scripts/OllamaHandler.cs` now stores dialogue output as an append-only history list in `Application.persistentDataPath/ollama-dialogue.json` instead of overwriting the last response.
- The saver migrates the previous single-entry JSON format into the new history wrapper on the next write.
- `docs/ollama-plan.md` was updated to describe the file as a history list.

**Why**:
The dialogue file needs to preserve prior turns so later systems can replay or inspect the generated conversation without losing earlier responses.

**Impact / docs touched**:
- Edited: `Assets/Scripts/OllamaHandler.cs`
- Edited: `docs/ollama-plan.md`
- Edited: `docs/refinements-changes.md` (this entry)

**Follow-ups**:
- Decide whether the history file should have a max length or rotation policy.
- Add a small reader API for NPC dialogue so consumers can load the history without duplicating JSON code.

## 2026-05-13 — Ollama test output now writes dialogue JSON
**Type**: refactor + decision
**AI tool(s)**: Cursor + Claude Opus 4.7

**What changed**:
- `Assets/Scripts/OllamaHandler.cs` now parses the Ollama `/api/generate` response, shows the extracted dialogue text in the TMP output field, and writes a structured JSON payload to `Application.persistentDataPath/ollama-dialogue.json` after each successful request.
- The saved payload includes the model name, original prompt, response text, and a UTC timestamp.
- `docs/ollama-plan.md` was updated to note the runtime JSON file so the dialogue flow stays documented.

**Why**:
The test UI previously only rendered the response and discarded it after the frame. Persisting the generated line gives us a concrete JSON handoff for later dialogue systems and makes the output reusable outside the temporary test UI.

**Impact / docs touched**:
- Edited: `Assets/Scripts/OllamaHandler.cs`
- Edited: `docs/ollama-plan.md`
- Edited: `docs/refinements-changes.md` (this entry)

**Follow-ups**:
- Decide whether the next dialogue system should append to a history file or overwrite the latest line.
- If we standardise on the JSON handoff, add a reader utility for NPC dialogue so other systems do not duplicate file I/O.

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
