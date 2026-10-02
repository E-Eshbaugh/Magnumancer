# Zombies ability compatibility — 2026-10-01

All eight wizards' three active runes and two passive runes have been reviewed against the goblin prototype. This pass fixes enemy targeting, health exchange, crowd control, hazards, defensive cover and co-op damage rules. Goblins now also have melee attacks, giving defensive and healing abilities a normal combat role. Wave progression is unchanged.

## Humans versus zombies

`ZombiesPoints` detects the scene's `GoblinSpawner` and calls `Teams.AllOneTeam()`. In this mode:

- Only damage attributed to a `GoblinHealth` can hurt a human. Teammate damage, self-damage and unowned/environmental damage are rejected inside `PlayerHealthControl.TakeDamage`, before shields, hit events or passives run.
- Friendly bullets, spells, poison, explosions and elemental reactions cannot apply harmful statuses, slow or knock back humans. Automatic offensive targeting excludes teammates.
- Human attacks still damage goblins; goblins cannot damage each other. Healing and movement abilities remain available to humans.
- Soul Swap exchanges percentages with goblins and bosses. In co-op, swapping with an enemy whose health percentage is no higher than the caster's fizzles, preventing self-harm or healing the enemy.
- Leaving the zombies scene restores free-for-all damage rules. These rules use the scene's spawner, rather than the menu's currently selected mode label.

## Active ability audit

| Wizard | Rune I | Rune II | Rune III |
|---|---|---|---|
| Granite Vow | Seismic Judgement resolves enemy roots, damages/stuns goblins and pushes them on the NavMesh; solid cover still blocks its damage. | Earthwork Parapet shoves/damages nearby goblins and carves temporary navigation cover; removal of the caster cleans up the tower. | Bastion Stance's six slabs carve navigation cover as they rise and follow aim. |
| Verdant Circle | Seed of Aloria heals human players in range. | Thornsnare damages and roots goblins. | Siphoning Vine selects goblins, drains them and heals the caster. |
| Tidebound | Shadow Clone enters the goblins' nearest-target search and absorbs one committed melee hit; enemies retarget when it pops or expires. | Undertow pulls and slows goblins, restoring their speed when they leave or the zone disappears. | Riptide damages/shoves goblins during the surge; the return remains caster movement. |
| Emberguard | Blazing Ruin hits goblin child colliders; its lava damages and slows goblins with correct multi-collider entry/exit cleanup. | Inferno Rounds refills ammo and applies its outgoing damage multiplier to goblins. | Fireball now locks onto goblins, detonates brands and leaves damaging lava. |
| Voltborn | Blinkstorm resolves enemy roots for damage/stuns; retains its existing monster damage multiplier. | Chain Surge selects and arcs between goblins, dealing damage and stun. | Stormrunner's blink fences shock and stun goblins. |
| Frostwarden | Covenant of Frostgrave shoves goblins from its footprint; navigation carving follows its rise and melt. | Winter's Wind now physically pushes goblins as well as damaging/chilling them. | Flash Freeze consumes goblin counters, damages and encases them. |
| Blightward | Viper's Nest detects enemy child hitboxes; poison damages each combatant once per tick. | Plaguebearer applies escalating plague damage to goblins. | Contagion infects goblins and spreads among enemies. |
| The Hollow | Soulfracture Beam resolves child hitboxes, marks before damage, and hits each enemy once; marked goblins burst on death. | Soul Swap supports goblin/boss health percentages and the co-op safety rule above. | Void Rift transports both humans and goblins; monster exits must be valid NavMesh positions and share the re-entry cooldown. |

## Passives and shared combat

All 16 passives use either caster stats or shared damage/kill events. Monster hit and kill credit already feeds Brand of Flereous, Fractalshot Shield, Lightning Reflex, Virulent Shroud, Cold Precision, Kindling, Leech Spores and Soul Harvest. Thornhide requires an attributed enemy attack. Stonebind, Fortified, Verdant Resurgence, Last Rites, Undercurrent, Tidal Momentum and Overcharge use the existing player health, movement or weapon hooks.

Goblin health now initializes before first-frame damage, reports actual damage rather than overkill, and awards death once even when a damage callback triggers another lethal hit. Kindling's cooldown refresh is immediate, and an ability's cooldown begins before activation so a kill during the cast can refresh it.

Shared queries resolve child colliders to their combatant root, ignore dead targets and grow their query buffer for large hordes. Freeze, stuns and named zone slows compose on goblin navigation speed. Pushes and pulls stay on the NavMesh and release navigation when the force ends.

## Enemy melee

`GoblinChaseNav`, already attached to the goblin and boss prefabs, now chases, winds up, strikes once, then recovers. Defaults are **12 damage**, **1.8m reach**, **0.45s windup**, **0.9s recovery**, a **100° forward arc**, and **1.1m flat-ground feet-height difference**. Grounded targets on a directly connected stair or ramp can be struck across a larger height difference; airborne targets and separate ledges retain the normal height limit. These are inspector fields; goblins and bosses currently share these first-pass attack numbers.

A growing orange ground wedge shows the committed direction and reach. The strike rechecks range, height, direction and solid cover at impact, so moving away, circling behind, jumping high enough, teleporting or raising cover can avoid it. Each swing hits only its original target. An expired or popped clone never transfers the pending hit to a human.

Stun, root, full freeze and knockback interrupt a pending swing; combat resumes after control returns and recovery ends. Partial chill and ordinary zone slows still affect pursuit speed. Death and disabling the AI cancel warnings and pending hits. `GoblinAnimationControl` holds the AI disabled until the existing spawn effect finishes.

Human damage goes through the existing health pipeline with the goblin as attacker. Shields, invulnerability, incoming damage reduction, hit passives and Thornhide retaliation therefore apply. Clones pop on impact without damaging their caster. No monster attack selects another monster, and the shared damage rules continue to enforce zero friendly fire.

## Movement and traversal

Pursuit disables destination braking, uses a small stopping distance, and refreshes moving destinations at most every 0.2 seconds. Acceleration and turning are tuned for continuous pursuit; avoidance priorities vary across the horde, and the locomotion animation blends speed changes over 0.12 seconds. Spell slows continue to own the agent's movement speed.

The Zombies scene rebuilds navigation from physics colliders at 0.1m voxel resolution with height detail, following the same collision ramps as wizards. Duplicate whole-scene surfaces with matching agent, layers and area are disabled after a successful replacement bake. Destructible props still carve navigation and reopen it when destroyed.

`GoblinTraversal` is attached automatically by the chase AI. Near a navigation edge it checks for a landing toward its target, then tests a full-body ballistic arc before hopping. Defaults allow a 2.5m apex and a 5m horizontal search, roughly matching the wizard's double-jump height. Zombies can jump onto navigable ledges and over low cover, including runtime carving obstacles. Landings must be on navigation: small prop tops omitted from the bake are not standing destinations. Tall walls, low ceilings, steep landings and unsupported gaps are rejected. New cover during flight cancels the jump safely; stun, root, freeze, knockback, death and disabling the AI release jump movement and restore navigation.

## Verification

`Assets/Editor/Tests/ZombiesAbilityChecks.cs` runs real Unity Play Mode physics and NavMesh checks from a batch editor. It builds a small test arena, uses production ability components, and writes `zombies-checks.txt` at the project root. Run only in a separate Unity instance with the project closed, or in an isolated test copy:

```text
Unity -batchmode -nographics -projectPath <project> -executeMethod ZombiesAbilityChecks.Run -logFile <log-path>
```

The latest 2026-10-01 run completed successfully: **118 assertions passed, zero gameplay errors**, using Unity 6000.6.3f1 in an isolated copy. Coverage includes ability/co-op behavior, melee, movement, traversal and the other existing gameplay checks. The temporary project's Unity Search index emitted one unrelated editor exception; the harness records that separately as an editor note. The production scripts also compile against the project's Unity references.

Coverage includes large hordes, child/multiple colliders, damage and statuses, nested passive deaths, targeting, cover lifecycle, navigation forces, clone retargeting, healing, portal transport, co-op damage immunity and restoration of deathmatch rules. These are functional checks, not a controller-driven balance or visual playtest.

Melee coverage drives the actual AI through pursuit, windup, impact and recovery, including dodges, new cover, interruption/resumption, clone consumption, spawn grace, death cancellation, shield absorption, invulnerability and lethal retaliation. Attack timing, telegraph readability in a crowd, and boss-specific tuning still need controller playtesting.

Navigation coverage checks steady pursuit, preserved spell slows, stair climbing and slope-aware melee, airborne dodges, full-size zombie ledge jumps, boss-sized vaults over runtime-carved cover, tall walls, low ceilings, jump interruption/resumption, restored navigation ownership, and duplicate-surface cleanup. Full-map crowding and controller feel still need playtesting.
