# Critical Engagement with Playtest Feedback

> Last updated: 2026-06-16.

Before attending the event, we anticipated that there would be some issues with how players would be able to interact with the quest given and the LLM integration. This anticipation mainly came from our own playtesting and the feedback received by our lecturer on part 2. However, during playtesting the reviewers had no comments on the narrative or the large language model integration which was surprising. It seemed that the reviewers were more concerned about the actual gameplay.

We also thought the visuals and textures would draw the most attention as these were also made by Cursor creating a unique look. What was interesting was both reviewers had comments on the lighting which we did not anticipate would be taken into consideration as it is typically a minor aspect of a game.

Where our expectations aligned with the feedback was with the readability and clarity concerns across the text and UI. The level design was mostly praised which we predicted as well. However, both reviewers suggested a need for a tutorial which before the feedback we felt that having key bind guidelines would be sufficient.

The reviewers also did not focus on character designs at all for the quest giver and enemies. We thought that this was a strong point of the game as these were made with Meshy AI and gives a unique charm to the game. We were also surprised that there were mixed opinions on the lighting one reviewer praised it, while the other said it did not help to guide the player in any way.

## What we chose to improve

- **Combat clarity**: We improved melee hit detection by using a sphere cast plus a close-range overlap probe so short dungeon foes reliably register hits. This reduces the “in range but not connecting” confusion noted in feedback.
- **Trap readability**: We improved spike trap legibility and fairness by making scattered spikes smaller and lower-profile, pairing them with a dedicated spike-trap material, and adjusting damage logic so jumping can clear them.
- **Text/UI readability**: We fixed TMP wrapping configuration (moving to `TextWrappingModes.Normal`) to improve legibility and reduce cases where text was harder to parse due to wrapping/overflow behaviour.
- **Lighting readability**: We increased readability-oriented lighting in the dungeon build. The mixed reviewer reaction suggests “nice mood” and “useful guidance” are not the same target, so this remains an active tuning area rather than “solved.”

## What we chose to ignore

- **A full tutorial flow**: Both reviewers asked for a tutorial. We did not implement a full in-game tutorial sequence because it would require additional UI states, trigger volumes, and level scripting beyond the current vertical-slice scope. We instead documented controls in `docs/setup.md` and focused iteration time on immediate clarity issues that blocked play (combat, traps, text).
- **Animation improvements**: The suggestion that animations need improvement was valid, but improving rigging/animation quality for Meshy-imported models would have required a deeper character pipeline (retargeting, animation controllers, and QA for URP materials). Given time constraints, we prioritised interaction clarity over animation polish.
- **Hiding/refining arrows**: One reviewer suggested arrows should be hidden. The current slice aims to keep navigation readable for first-time players; we did not implement a full alternative guidance system (subtle signage, lighting breadcrumbs, diegetic landmarks) to replace explicit direction cues yet. This is a design trade-off: removing guidance without a replacement risks increasing frustration in a maze.

## Evaluation of feasibility

- **Toolset feasibility (Unity + Ollama + local inference)**: The requested gameplay clarity changes were feasible and largely independent of the LLM pipeline, so they were good targets for iteration. In contrast, animation quality improvements were feasible in Unity but not within the project’s current asset pipeline and timebox.
- **Core-experience alignment**: We chose changes that improved fairness and readability without changing the core experience (a navigable maze slice with cosy-fantasy flavor). The LLM remained an atmosphere layer, not a gatekeeper for combat, traps, or quest progression.

## Final judgement

At the event, we expected most feedback to be about quest interaction and LLM integration, because those were the areas we had already flagged in our own testing and earlier lecturer feedback. Surprisingly, reviewers barely commented on narrative or AI. Instead, they focused on core gameplay clarity.

Although reviewers did not raise LLM issues directly, our main post-feedback work was still on the model pipeline. We moved Cap’s prompts to a Jinja2 template (`cap_personality.jinja2`) so personality and quest context are built more reliably, added stronger filtering for qwen3 “planning text” leaking into dialogue, warmed Ollama on the main menu to cut cold-start delay, and fixed Ask Cap so replies overwrite cleanly instead of stacking. We also made smaller gameplay tweaks (combat hits, enemy AI) alongside this.

We did not add a full tutorial or animation overhaul — both were out of scope for this stage. This taught us that even when feedback targets gameplay, the LLM layer still needs its own iteration to stay invisible and usable.
