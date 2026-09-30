# Destructible Environment

> **Status (2026-09-30):** first pass **built**: destructible props with health bars, crumble and explode deaths, element weaknesses, and persistent craters and cracks. Not playtested yet.

## Goal

The arena should visibly take a beating. Cover wears away as the match goes on, so late-match fights get more open and more frantic, and big moments leave marks (craters, cracks, rubble) that tell the story of the fight.

## What's built

### Destructible props (`Assets/Scripts/Environment/Destructible.cs`)

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

- **Never destructible** (the arena's shape and landmarks): anything named wall, building, stairs, ground, floor, terrain, plane, ramp, lava, water, lake, river, light, backdrop, progress, environment, bridge, tower or castle. Also anything with a footprint over **9m**, and gameplay objects (players, monsters, ice walls, crystals, Zombies progress walls, mines).
- **Health** = 20 + 35 × the prop's bounds size (the diagonal), × toughness, capped at **400**. Roughly: a small rock ~70, a rubble pile ~150, a tree ~300 or more.
- Props you place by hand with a `Destructible` component keep their own settings.
- On load, the console logs how many props each map made destructible.

Per map (from the prop survey), this catches: Frostgrave's 58 rubble piles; Stormspire's 60 rubble and pillars; Oldwoods' ~80 trees, rocks and fences; Fungal Hollow's ~40 mushrooms and trees; Cinder Crucible's rocks, pillars and rubble; Drowned Sanctum's 7 barrels; BlackOsuary's columns and kegs; Riftforge's fences; and the Zombies map's tables, barrels, crates, chests and bottles.

### Craters and cracks (`Craters.cs`)

- Any blast of **26+ damage** (Combust, fireball, barrels, grenades, Blinkstorm) leaves a **crater**: a soot ring, a darker pit and a rim of half-buried rubble. It's aligned to the floor and only placed on floors (not walls or steep slopes).
- **Seismic Judgement** splits the ground into radial **cracks**.
- They stay for the match. There's a cap of **40**; the oldest flatten and fade.
- Purely visual: no colliders, no navmesh changes.

## Ideas for the next pass

1. **Real craters in Terrain maps** (Cinder Crucible, Drowned Sanctum, Fungal Hollow, Riftforge): lower `TerrainData` heights in a bowl at the blast. The terrain data must be cloned at match start so the asset on disk isn't changed. Needs care with player grounding (CharacterController) and the lava planes.
2. **Damage states**: swap to a cracked look at 50%, or tint darker like `IceWallEffect.ShowDamage` does, so health reads without the bar.
3. **Partial walls**: KayKit has `wall_half`, `wall_broken` and `rubble_*` models. A destroyed wall could swap to a broken wall plus rubble (still some cover) rather than vanishing. Arena boundary walls should stay indestructible.
4. **Rubble as cover**: big props leave a low rubble pile (half-height cover that also breaks).
5. **Burning props**: fire could leave wood burning (a damage-over-time and a Wildfire source), and frost could make stone brittle.
6. **Zombies**: a rebake isn't possible at runtime, so props that are baked into the navmesh (not carving obstacles) keep their hole after breaking. Consider marking them as carving obstacles instead of baked.
7. **Bullet holes and scorch marks** on floors and walls (a cheap, capped decal pool).
