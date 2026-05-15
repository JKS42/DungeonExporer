# Art Direction — DungeonExporer

> Single source of truth for the visual style of the game and the prompts / tools that produced each asset.
> Tone: **lighthearted fantasy** — warm parchment, sun-gold, mossy green, cocoa brown, brass. No black, no grimdark.
> Last updated: 2026-05-15

## Style pillars

1. **Cosy, not creepy.** A dungeon explorer that feels like rummaging through a friendly grandparent's attic, not a horror set.
2. **Hand-painted, not photorealistic.** Painterly textures, soft baked lighting. PBR is allowed for metal accents (brass), but avoid the "shiny plastic" look.
3. **Warm palette only.** Parchment cream, sun-gold, mossy green, cocoa brown, brass. Reds reserved for the destructive UI accent (Quit, danger). Black is never the dominant value — use deep cocoa or twilight purple instead.
4. **Slightly stylised proportions.** Characters: head ≈ 1/6 body height, expressive cartoon features. Environments: chunky silhouettes, readable shapes from a distance.
5. **Readable silhouettes.** Every hero asset (player, NPC, key prop) must read in one beat as a flat black silhouette.

The palette is mirrored in code at `Assets/Scripts/UI/MenuTheme.cs` so UI and 3D art stay in sync.

## Assets

### Player character — "The Adventurer"

- **Location**: `Assets/Art/Characters/Adventurer/`
- **Files**:
  - `Adventurer.fbx`
  - `Adventurer_BaseColor.png`
  - `Adventurer_Metallic.png`
  - `Adventurer_Normal.png`
  - `Adventurer_Roughness.png`
- **Tool**: [Meshy AI](https://www.meshy.ai/) — text-to-3D, "Stylized" preset, symmetric, T-pose, quad topology.
- **Generated**: 2026-05-13.
- **Status**: First-pass concept. Not yet rigged, not yet imported into a scene.

#### Prompt used (Meshy-safe, ~750 chars)

> Stylized full-body 3D character for a cosy fantasy dungeon-crawler: cheerful young adventurer, slightly chibi proportions, friendly cartoon face. Brown leather jerkin over cream linen tunic, mossy-green trousers, sturdy brown boots with rolled tops, wide leather belt with brass buckle, small hip satchel, floppy wide-brimmed travel hat with a tucked feather. Small brass lantern on belt; short sword sheathed across the back. Tousled chestnut hair, warm amber eyes, rosy cheeks. Warm palette: parchment cream, sun-gold, mossy green, cocoa brown, brass — no black, no grimdark. Hand-painted painterly texture, no photorealism. T-pose, arms straight out, legs slightly apart, facing forward, symmetric, full body, white background, clean game-ready topology.

#### Negative prompt

> photorealistic, dark, grimdark, horror, asymmetric, action pose, multiple characters, background scenery, watermark, text, extra limbs, broken topology, low resolution

#### Meshy settings (re-roll with the same prompt to reproduce)

| Setting | Value |
|---|---|
| Art Style | Cartoon / Sculpture-Stylized |
| Symmetry | On |
| Pose | T-pose |
| Topology | Quad |
| Target polycount | 10k–20k tris |
| PBR Textures | On (for the brass accents) |

### Dungeon brick — tileable wall albedo

- **Location**: `Assets/Art/Environment/DungeonBrick/`
- **Files**:
  - `DungeonBrick_Albedo.png` — 1024×1024 tiling brick pattern (warm mortar, cocoa / terracotta bricks, light noise).
  - `DungeonBrickWall.mat` — URP Lit (base map only); used by `DungeonLevelBuilder` on wall cubes with per-renderer UV scale via `MaterialPropertyBlock`.
- **Tool**: **Python 3 + Pillow** — procedural PNG (running-bond layout, row offset, per-brick hue jitter, thin joints, sparse soften noise).
- **Generated**: 2026-05-15 (regenerated with moss + crack pass).
- **Status**: In use on **Level1** via **Dungeon** → `_wallMaterial`.

#### “Prompt” / recipe (reproduce the same look)

> Tileable square **albedo** for a **cosy fantasy dungeon** wall: **running-bond** bricks, **parchment-warm mortar** joints, brick faces in **cocoa** and muted **terracotta**, soft hand-painted variation (not photoreal). Light edge shading on brick courses, sparse **moss** patches, hairline **cracks**. Stay in the warm palette — no crushed blacks, no grimdark.

#### Generation notes (Pillow)

| Setting | Value |
|---|---|
| Script | `Tools/generate_dungeon_textures.py` → `make_brick_wall()` |
| Canvas | 1024 × 1024, RGB PNG |
| Bond | Running bond with row horizontal offset |
| Palette | Mortar ~RGB(198,186,168); bricks varied cocoa / terracotta |

### Dungeon floor — tileable flagstone albedo

- **Location**: `Assets/Art/Environment/DungeonFloor/`
- **Files**:
  - `DungeonFloor_Albedo.png` — 1024×1024 rounded flagstones, warm mortar gaps.
  - `DungeonFloor.mat` — URP Lit; **`DungeonLevelBuilder._floorMaterial`** with per-cell tint (safe / encounter) via `MaterialPropertyBlock`.
- **Tool**: **Python 3 + Pillow** — `make_floor()` in `Tools/generate_dungeon_textures.py`.
- **Generated**: 2026-05-15.
- **Status**: In use on **Level1** walkable floors.

### Spike trap — tileable hazard albedo

- **Location**: `Assets/Art/Environment/SpikeTrap/`
- **Files**:
  - `SpikeTrap_Albedo.png` — 512×512 rusted grate + upward spikes (top-down readable).
  - `SpikeTrap.mat` — URP Lit; assigned on **`LevelGameplayBootstrap._spikeTrapMaterial`**.
- **Tool**: **Python 3 + Pillow** — `make_spike_trap()` in `Tools/generate_dungeon_textures.py`.
- **Generated**: 2026-05-15.
- **Status**: In use on scattered maze spike hazards.

## First-time Unity import — manual step required

Because the textures were renamed for clarity, Unity's FBX importer may not auto-link them to the embedded material. After Unity finishes importing the new files, do the following **once**:

1. In the Project window, select `Assets/Art/Characters/Adventurer/Adventurer.fbx`.
2. In the Inspector, open the **Materials** tab.
3. Click **Extract Materials…** and pick `Assets/Art/Characters/Adventurer/`. A new material will appear (e.g. `Adventurer_mat.mat`).
4. Click **Extract Textures…** and pick the same folder. (If the textures are already in the folder, Unity will reuse them.)
5. Select the extracted material. In the Inspector:
   - **Base Map** → `Adventurer_BaseColor.png`
   - **Metallic Map** → `Adventurer_Metallic.png`
   - **Normal Map** → `Adventurer_Normal.png` (tick "Fix Now" if Unity prompts about marking it as a normal map)
   - **Smoothness Source** → "Metallic Alpha" (default) or remap from Roughness if needed:
     - Drop `Adventurer_Roughness.png` into the **Smoothness** slot only if you've inverted it; otherwise leave Smoothness driven by Metallic Alpha.
6. The model should now look correct in the Scene view.

Once this is done, do **not** rename these files again — the material will reference them by GUID.

## Tools roster

| Tool | Used for |
|---|---|
| **Meshy AI** | Text-to-3D character + prop generation |
| **Mixamo** *(planned)* | Auto-rig + animation library for humanoid characters |
| **Unity URP** | Real-time rendering, lighting, material setup |
| **Python (Pillow)** | Procedural tileable 2D textures (e.g. dungeon brick albedo) |

When we add a new asset, append it to the table above and add a section under **Assets** with the prompt and the tool's settings — same shape as the Adventurer entry. This keeps every art decision reproducible.
