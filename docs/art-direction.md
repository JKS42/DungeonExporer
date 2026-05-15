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

### Dungeon stone wall — tileable albedo (folder name `DungeonBrick`)

- **Location**: `Assets/Art/Environment/DungeonBrick/`
- **Files**:
  - `DungeonBrick_Albedo.png` — 1024×1024 **stylized irregular stone blocks**, dark mortar, top-left highlights (cartoon dungeon wall).
  - `DungeonBrickWall.mat` — URP Lit; `DungeonLevelBuilder._wallMaterial` with UV scale via `MaterialPropertyBlock`.
- **Tool**: **Python 3 + Pillow** — `make_stone_wall()` in `Tools/generate_dungeon_textures.py`.
- **Generated**: 2026-05-15 (stone pass; replaces running-bond brick).
- **Status**: In use on **Level1** walls.

#### “Prompt” / recipe (reproduce the same look)

> Tileable **stylized fantasy dungeon wall** (reference: large hand-drawn stone blocks): **few, big irregular stones**, **thick dark charcoal-brown mortar**, warm **grey/taupe** faces, strong **top-left highlights** and **bottom-right shade**, **pockmarks** and **edge chips**. Not running-bond brick; readable at gameplay scale.

#### Generation notes (Pillow)

| Setting | Value |
|---|---|
| Script | `make_stone_wall()` |
| Canvas | 1024 × 1024, RGB PNG |
| Layout | ~7×9 jittered irregular quads per tile |
| Palette | Mortar ~RGB(108,98,86); stones varied warm grey-brown |

### Dungeon floor — tileable stone slab albedo

- **Location**: `Assets/Art/Environment/DungeonFloor/`
- **Files**:
  - `DungeonFloor_Albedo.png` — 1024×1024 large **irregular stone slabs**, worn centres, grout gaps.
  - `DungeonFloor.mat` — URP Lit; **`DungeonLevelBuilder._floorMaterial`** with per-cell tint (safe / encounter) via `MaterialPropertyBlock`.
- **Tool**: **Python 3 + Pillow** — `make_stone_floor()` in `Tools/generate_dungeon_textures.py`.
- **Generated**: 2026-05-15 (stone slab pass).
- **Status**: In use on **Level1** walkable floors.

#### Recipe

> Tileable **fantasy dungeon floor**: large **irregular paving slabs**, dark **grout**, lighter worn patches on slab centres, fine grit and short cracks. Warm grey-brown stone — readable at gameplay scale.

### Spike trap — tileable hazard albedo

- **Location**: `Assets/Art/Environment/SpikeTrap/`
- **Files**:
  - `SpikeTrap_Albedo.png` — 512×512 rusted grate + upward spikes (top-down readable).
  - `SpikeTrap.mat` — URP Lit; assigned on **`LevelGameplayBootstrap._spikeTrapMaterial`**.
- **Tool**: **Python 3 + Pillow** — `make_spike_trap()` in `Tools/generate_dungeon_textures.py`.
- **Generated**: 2026-05-15.
- **Status**: In use on scattered maze spike hazards.

### NPC quest-giver — "Cap"

- **Location**: `Assets/Models/Meshy_AI_Stylized_full_body_3D_0515132850_texture_fbx/…/Meshy_AI_Stylized_full_body_3D_0515132850_texture.fbx` (rename when stabilizing imports).
- **Tool**: [Meshy AI](https://www.meshy.ai/) — Cartoon / Sculpture-Stylized, T-pose, quad, ~12k–22k tris.
- **Generated**: 2026-05-15.
- **Status**: Spawned at runtime by **`LevelGameplayBootstrap`** on nearest **S** cell (`DungeonLevelBuilder.TryGetNpcHubPosition`). Target height ~1.65 m.

#### Prompt used (Meshy-safe)

> Stylized full-body 3D character for a cosy fantasy dungeon-crawler: friendly older quest-giver NPC named Cap, veteran dungeon guide with a storyteller vibe. Slightly stocky build, shorter than a hero adventurer, warm grandfatherly cartoon face, kind squinting eyes, neat trimmed grey beard, rosy cheeks, gentle smirk. Soft mossy-green wool coat over parchment-cream shirt, cocoa-brown trousers, sturdy tan boots. Distinctive short brimmed cap (newsboy / traveller cap) in sun-gold with a small brass pin. Leather satchel strap, brass-rimmed spectacles pushed up on forehead, rolled parchment maps peeking from coat pocket, tiny brass lantern charm on belt, wooden clipboard with blank papers in one hand (empty, no readable text). Warm palette only: parchment cream, sun-gold, mossy green, cocoa brown, brass — no black, no grimdark. Hand-painted painterly texture, soft shading, no photorealism, no shiny plastic. Readable chunky silhouette. T-pose, arms slightly out, legs slightly apart, facing forward, symmetric, full body, white background, clean game-ready quad topology.

#### Negative prompt

> photorealistic, young teen, child, warrior armor, huge weapon, sword drawn, action pose, running, fighting stance, monster, undead, grimdark, horror, evil grin, scary, asymmetric, multiple characters, background scenery, dungeon room, watermark, text, letters, logo, extra limbs, broken topology, low resolution, neon colours, pure black clothing

### Enemy — "Grumblemite" (DungeonFoe)

- **Location**: `Assets/Models/Meshy_AI_Stylized_full_body_3D_0515132512_texture_fbx/…/Meshy_AI_Stylized_full_body_3D_0515132512_texture.fbx`
- **Tool**: Meshy AI — same preset as Cap; target ~8k–15k tris (smaller creature).
- **Generated**: 2026-05-15.
- **Status**: Scattered on **E** encounter cells via **`DungeonLootScatter`**; **`EnemyActor`** + combat capsule (~1.1 m height).

#### Prompt used (Meshy-safe)

> Stylized full-body 3D creature for a cosy fantasy dungeon-crawler: squat mischievous dungeon pest called a Grumblemite, about waist-high to a human adventurer. Chunky round body, short stubby legs, big expressive cartoon eyes (slightly cross or grumpy, not scary), small chipped horns, oversized hands with blunt claws. Worn leather chest strap, patched burlap loincloth, one broken brass ear-ring. Muted terracotta and cocoa-brown skin with mossy-green patches, parchment-cream belly, warm rust accents — danger colours only as accents, not horror. Hand-painted painterly texture, soft shading, no photorealism, no shiny plastic. Readable chunky silhouette from above. T-pose, arms slightly out, legs apart, facing forward, symmetric, full body, white background, clean game-ready quad topology.

#### Negative prompt

> photorealistic, realistic human, grimdark, horror, gore, zombie, skeleton, demon, dragon, giant, spider, flying, weapon in hand, action pose, asymmetric, multiple characters, background scenery, watermark, text, extra limbs, broken topology, low resolution, neon colours, pure black body, scary teeth, blood

### World pickups — bubble orbs (code-driven, no mesh)

- **Implementation**: `PickupBubbleVisual` — URP transparent Lit bubble + billboard quad icon (procedural cross = heal ration, pebble dot = loot).
- **Status**: Pebbles and trail rations scattered by **`DungeonLootScatter`**.

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
| **Python (Pillow)** | Procedural tileable 2D textures (`Tools/generate_dungeon_textures.py`) |

When we add a new asset, append it to the table above and add a section under **Assets** with the prompt and the tool's settings — same shape as the Adventurer entry. Prefer stable paths under `Assets/Art/<Category>/<Name>/`; Meshy exports may live under `Assets/Models/` until renamed.

**Import note:** Unity `.meta` GUIDs must be exactly **32** hexadecimal characters. Invalid GUIDs break materials and scene references (see `docs/setup.md` troubleshooting).
