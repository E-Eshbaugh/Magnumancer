# Destructible Environment

> **Status (2026-09-30):** two passes **built**: destructible props with health bars, damage states, burning and brittle props, crumble/explode/stub deaths, destructible interior walls, craters (real dents on Terrain maps), cracks, bullet holes and scorch marks, and Zombies nav that opens up as props break. Not playtested yet.

## Goal

The arena should visibly take a beating. Cover wears away as the match goes on, so late-match fights get more open and more frantic, and big moments leave marks (craters, cracks, rubble) that tell the story of the fight.

## Per match, map tour

Matches are a **tour**: every match is a different map, and the map **fully resets** each match (Gang Beasts style; see [Modes & Match Flow](modes-and-flow.md)). So destruction only has to last one match, and wearing a map down is the point: a forest fire or a flattened dungeon is a one-match spectacle, not permanent damage.

- **Pacing target:** cover should mostly survive the opening, be about half gone by the middle of a 5–10 minute match, and leave the endgame open and frantic. Tune with per-rule toughness in `DestructibleSetup.Rules`.
- **Everything resets on map load:** props, stubs, craters, marks, burning, terrain dents (each match digs a fresh copy of the terrain, and the previous match's copy is freed), health bars, element zones, Zombies points and teams.

### Adding a map to the tour

Destruction sets itself up from names, so new maps mostly just work if they follow these conventions:

- Build props from the KayKit / Nature MegaKit / mushroom models and keep their model names (`rubble_large`, `pillar`, `CommonTree`, `barrel_large`...) so the name rules catch them. Give each a convex collider.
- Name wall pieces `wall...`. The outer ring is detected as the boundary and never breaks. Name joints `wall_corner` / `wall_crossing`.
- Use `building_`, `stairs`, `tower`, `bridge`, `castle` for landmarks that should never break. Anything over 9m across never breaks either.
- For hand-tuned props, add a `Destructible` component yourself: it's kept as placed.
- Use a Unity **Terrain** floor if blasts should dent the ground (mesh floors get decal craters only).
- For map water or lava, add a `MapElementZone` with its own collider covering only where players stand in it (see [Elemental Ecosystem](elemental-ecosystem.md), "Needs the editor").
- Check the console on first play: `[Destructibles] N props in <map> can be destroyed`.

## What's built

### Destructible props (`Assets/Scripts/Environment/Destructible.cs`)

- **Shotgun slugs (LT) are breaching rounds:** ×4 a full buckshot volley against props, walls, ice walls and crystals, but only 40% against players, on a 3.5s cooldown (see [Balance Log](balance-log.md), Pass 9).
- **Damage:** every bullet that hits a prop (with the shooter's element) and every explosion (`Explosions.AffectWorld`, with falloff and a short delay that ripples outward).
- **Element weaknesses:** fire ×2 against wood, plants and ice; poison ×1.5 against plants; earth ×1.5 against stone and crystal; lightning ×1.5 against metal; void ×1.5 against bone; frost ×0.5 against ice (`Destructible.ElementMultiplier`). Weak hits spray more chips.
- **Hit feedback:** chips fly off where it was hit, and the prop jolts (skipped on static-batched meshes, which can't move).
- **Health bar** (`PropHealthBar.cs`): the same look as the wizards' bars (frame, trailing chip that drains, flash, jolt, a pulse below 30%) in a neutral bone color. It **only appears while the prop is being hit** and fades out after **2.5s** without damage. Its width scales with the prop's size. It's drawn under the wizard bars.
- **Death:**
  - **Crumble:** chunks tinted to the prop (stone grey, wood brown, Fly Agaric red...) burst out, a dust cloud rises, crystal and ice shatter with a flash, plants shed spores, then the prop slumps into the ground and is gone. Colliders turn off immediately, so bullets and players pass through.
  - **Explode** (barrels, kegs): real area damage credited to whoever broke it, the explosion shove, mines, grenades and other barrels set off (chains), gas clouds and brambles ignited (Combust / Wildfire), and a crater.
  - In Zombies, breaking a prop turns off its carving `NavMeshObstacle`, so goblins can path through the gap.
- `Destructible.Destroyed(prop, attacker)` fires for every broken prop (for points, announcer lines or challenges later).

### Auto-setup (`DestructibleSetup.cs`)

No scene edits needed. When a gameplay map loads (one with a `WinManager` or `GoblinSpawner`), every solid collider whose object name matches a rule becomes destructible:

| Rule (name contains) | Material | Toughness | Notes |
|---|---|---|---|
| barrel, keg | Wood | ×0.6 | **Explodes**: 35 damage, 3.5m |
| box_stacked, crate, chest, trunk, table, chair, fence_wood, catapult, trees_A_cut | Wood | ×1 | |
| CommonTree, Pine, DeadTree, TwistedTree | Wood | ×1.4 | |
| rubble, rock, resource_stone, pillar, column, fence_stone, barrier, statue, tomb | Stone | ×1.3 | |
| Fly_Agaric (red debris), Inky_Cap (pale debris), mushroom, plant, bush | Plant | ×0.7 | |
| bottle | Crystal | ×0.2 | |
| crystal | Crystal | ×0.8 | |
| skull, bone | Bone | ×0.6 | |

- **Never destructible** (the arena's shape and landmarks): boundary walls and wall joints (see the second pass; interior walls *are* destructible), and anything named building, stairs, ground, floor, terrain, plane, ramp, lava, water, lake, river, light, backdrop, progress, environment, bridge, tower or castle. Also anything with a footprint over **9m**, and gameplay objects (players, monsters, ice walls, crystals, Zombies progress walls, mines).
- **Health** = 20 + 35 × the prop's bounds size (the diagonal), × toughness, capped at **400**. Roughly: a small rock ~70, a rubble pile ~150, a tree ~300 or more.
- Props you place by hand with a `Destructible` component keep their own settings.
- On load, the console logs how many props each map made destructible.

Per map (from the prop survey), this catches: Frostgrave's 58 rubble piles; Stormspire's 60 rubble and pillars; Oldwoods' ~80 trees, rocks and fences; Fungal Hollow's ~40 mushrooms and trees; Cinder Crucible's rocks, pillars and rubble; Drowned Sanctum's 7 barrels; BlackOsuary's columns and kegs; Riftforge's fences; and the Zombies map's tables, barrels, crates, chests and bottles.

### Craters and cracks (`Craters.cs`)

- Any blast of **26+ damage** (Combust, fireball, barrels, grenades, Blinkstorm) leaves a **crater**: a soot ring, a darker pit and a rim of half-buried rubble. It's aligned to the floor and only placed on floors (not walls or steep slopes).
- **Seismic Judgement** splits the ground into radial **cracks**.
- They stay for the match. There's a cap of **40**; the oldest flatten and fade.
- Purely visual: no colliders, no navmesh changes.

## Second pass (built 2026-09-30)

### Damage states
- A prop **darkens as it weakens** (tint by `MaterialPropertyBlock`, down to 55% brightness). At **half health it cracks**: a burst of dust and chips and a slight lean. Health reads without the bar.

### Stubs: walls and big props
- Walls, tall stone props (taller than 2.2m: pillars, statues) and trees **break in two stages**. The first "death" knocks the top off in a shower of debris, and a **low stub** stays behind at 35% of the height (trees leave a 12% stump) with 40% health. It's still half cover. The second death clears it.
- **Interior walls are now destructible** (stone, toughness ×2.2, with a stub). **Boundary walls never break**: any wall within 3m of the outer edge of all the map's wall pieces counts as boundary (`DestructibleSetup.BoundaryMargin`). Wall joints (corners, crossings) never break. **No walls break in Zombies** (they gate progress).

### Fire and frost on props
- **Fire sets wood and plants burning** for 4s (refreshed by more fire). A burning prop takes 8 damage/s, chars darker, sheds embers and smoke, and each second has a 30% chance to **spread** to flammable props within 1.5m, so forests can go up. A burning prop **is a fire zone**: it ignites poison gas (Combust) and brambles (Wildfire) it touches, and a water zone reaching it (Undertow) **puts it out** with a steam burst.
- **Exploding barrels set nearby wood alight.**
- **Frost makes stone and crystal brittle** for 4s: an icy tint, and ×1.5 damage from everything but frost. Frostbreaker-style play: chill the cover, then smash it.

### Real terrain craters (`TerrainCraters.cs`)
- On the Terrain maps (Cinder Crucible, Drowned Sanctum, Fungal Hollow, Riftforge), big blasts **push the ground down into a bowl** with a raised lip: 0.12m deep per metre of blast radius, up to 0.5m per crater, and never more than **1.2m** below the original ground however many blasts land. The scorch decal sits down in the pit.
- Each match digs into **its own copy** of the terrain data, made at load, so the map asset on disk never changes. Collision catches up once per frame.

### Bullet holes and scorch marks (`ImpactMarks.cs`)
- Every bullet that hits the world (floors, walls; not people, props or mines) leaves a mark sized by damage. **Fire rounds scorch** wider and browner, **frost** leaves a pale frost patch, **poison** a sickly stain, **lightning** a dark burn. A pool of **220**; the oldest are reused.

### Zombies navigation
- The baked navmesh has holes where props stood, and a baked hole can't reopen at runtime. So at load the Zombies navmesh is **rebuilt once without the destructible props** (same settings as the original bake), and every destructible prop gets a **carving obstacle**. Breaking a prop turns its obstacle off and the path opens.
- **In standalone builds this needs Read/Write enabled** on the prop models' import settings, because the surface builds from render meshes (they're currently all off). In the editor it just works. Set `DestructibleSetup.RebuildZombiesNavmesh = false` to skip it.

## Still to explore

- Cracked-mesh swaps: KayKit ships `wall_broken`, `rubble_*` and half-wall models that could replace the scaled stub for a nicer look (they'd need to be in a Resources folder or referenced by an asset).
- Players standing next to a burning prop catching fire.
- Painting terrain craters with a scorched terrain layer instead of a decal.
- Physics debris that stays (rubble piles as new low cover).
