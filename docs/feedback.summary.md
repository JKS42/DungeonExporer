# Playtest Feedback Summary

> Consolidated reviewer feedback from Level1 playtesting.  
> Last updated: 2026-06-18.

---

## Reviewer 1

### Background

- Has played games since age 8.
- Favourite genres: horror and cutesy.
- Has played Five Nights at Freddy’s and Fortnite.
- Gender: Female

### Expected / Main Feedback

- Lighting was good.
- Directions and maze design were good, but arrows should be hidden.
- Game needs a tutorial.

### Surprised / Reaction Notes During Play

- Surprised by overall lighting and navigation flow.

---

## Reviewer 2

### Background

- Reviewed ~30 past projects.
- 5 years in game development.
- Favourite genres: narrative and story-driven games.
- Gender: Male

### Expected / Main Feedback

- Spike traps are not clear.
- Animations need improvement.
- Enemy and player attack radius needs adjustment/clarity.
- Text is hard to read.
- Lighting is not helping the player.
- Game needs a tutorial.

### Surprised / Reaction Notes During Play

- Surprised that lighting did not support player readability/navigation.

---

## Project Aspects Addressed by the Feedback

| Area | Notes |
|---|---|
| LLM integration / narrative | No direct comments in this feedback set. |
| Gameplay mechanics | Attack radius balance/clarity; spike trap readability. |
| UI/UX | Text readability; tutorial onboarding. |
| Level design / navigation | Maze directions praised; arrows should be hidden/refined. |
| Visuals / presentation | Lighting quality and usefulness; animation quality. |
| Accessibility / readability | Text clarity and hazard clarity. |

---

## Recurring Themes (Raised Multiple Times)

- Tutorial/onboarding is needed (mentioned by both reviewers).
- Lighting needs to better support player understanding (positive and negative angles both raised).
- Readability/clarity issues (text readability, trap clarity, combat range clarity).

---

## Initial Reactions While Receiving Feedback (Unbiased Capture)

- Noted mixed feedback on lighting (one reviewer praised it, another said it does not help player guidance).
- Noted repeated requests for a tutorial (clear high-priority pattern).
- Noted that readability/clarity concerns appear across multiple systems (UI text, hazards, combat ranges).
- Noted positive response to maze direction and flow, with suggestion to hide explicit arrows.

---

## Responses implemented (post-playtest)

See [`critical.feedback.md`](./critical.feedback.md) for rationale and [`refinements-changes.md`](./refinements-changes.md) for dated engineering entries. Summary:

| Feedback theme | Response |
|---|---|
| Combat range / hit clarity | Stronger `PlayerCombat` detection; swing/impact VFX; HUD crosshair pulse on hit |
| Spike trap clarity | Regenerated spike albedo, emissive hazard material, `HazardTrapVisual` marker |
| Text readability | `TmpTextUtility`, larger black gameplay fonts, UI shadows/outlines on signs |
| Lighting | Torch placement rework + coverage pass in `DungeonLevelBuilder` |
| Tutorial / onboarding | **Main Menu → How to Play** panel (partial); full tutorial deferred |
| LLM (not raised by reviewers) | C# Cap template, request queue, fast mode, warm-up, AI side quests, quest completion HUD, planner deferral; Ask Cap removed |
