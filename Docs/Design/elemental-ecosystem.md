# Elemental Ecosystem

> **Status (2026-09-30):** the core is **built**: the element model, all statuses, the reaction system, element sources (bullets, abilities, zones) **every reaction in §2** (Conduct, Steam, Shatter, Combust, Thermal Shock, Brittle, Mudslide, Magnetize, Wildfire, Blight Bloom, Overgrowth Surge, Echo), the per-player **combo counter**, and the environment rules (lava + water, fire vs ice walls, explosions shove, a map water/lava component), all with feedback. Not playtested yet. See [§6 Implementation](#6-implementation-built-2026-09-30) for what exists and how it behaves. Numbers are in the [Balance Log](balance-log.md) (Passes 3–5).

> **Core idea:** elements aren't team-only synergies. They're an **ecosystem**. Every wizard *applies* an elemental status and every wizard can *set off* reactions on statuses others applied, **no matter who applied them**. In free-for-all that means accidental combos, stolen kills and chaos. In team modes it means planned combos and team comps.

## 1. Statuses (what each element leaves on a target)

| Element | Wizard | Status | How it's applied | Lasts |
|---|---|---|---|---|
| Fire | Emberguard | **Burning / Branded** | Bullets brand (exists), lava, fireball, fire dash | Brands decay 4s |
| Frost | Frostwarden | **Chilled**, then **Frozen** at 5 counters | Bullets add freeze counters (exists); Flash Freeze encases | Counters decay 3s |
| Water | Tidebound | **Soaked** *(new)* | Tidebound bullets (chance per hit), Riptide, Undertow, water zones, map water | ~5s |
| Lightning | Voltborn | **Charged** *(new)* | Voltborn bullets, Chain Surge, fences | ~3s (short and snappy) |
| Earth | Granite Vow | **Staggered** *(new tag on stun and knockback)* | Seismic Judgement, Rockslide, heavy knockback | ~1s |
| Nature | Verdant Circle | **Rooted / Overgrown** | Thornsnare, Overgrowth, Siphoning Vine | While rooted |
| Poison | Blightward | **Poisoned** | Mines, poison clouds, Plague, Contagion, puddles | While in cloud, plus ~3s |
| Void | The Hollow | **Marked** | Soulfracture Beam (exists as the void mark) | Until triggered |

**Bullets carry their wizard's element** (bullet colors and trail flavors already match via `BulletFX.FlavorOf`). Wizards that don't already apply a status on hit get a light chance per hit, or a buildup meter, so reactions come up naturally mid-fight and not only from abilities.

## 2. Reactions (a status meets another element)

Reactions **consume** the status (no infinite loops). **Credit goes to whoever triggered them.** They work on anyone, including the triggerer's own teammates in free-for-all.

| Reaction | Recipe | Effect | Feel |
|---|---|---|---|
| **Steam** | Fire + Water | A 3s scalding steam cloud at the target that hides anyone inside it (bullets fly through, you just can't see who's in there); small burn damage | Smoke-bomb chaos |
| **Conduct** | Water + Lightning | Lightning jumps to **every Soaked target** within ~8m, with a short stun on each. Electrified water zones shock anyone standing in them | The big team combo |
| **Shatter** | Frozen + Earth (or any heavy knockback) | Bonus damage (scales with counters) and the target is knocked out of the ice | Satisfying crunch |
| **Thermal Shock** | Chilled/Frozen + Fire | Cashes in freeze counters as burst damage (about 6 per counter) and clears brands | Hot/cold combo |
| **Combust** | Fire + Poison **cloud** | The gas cloud **explodes** (area damage plus knockback) and leaves a burning patch | Shooting poison with fire = boom |
| **Wildfire** | Fire + Nature | Vines, thorns and overgrowth ignite into a spreading burn zone | Risky: burns Verdant allies too |
| **Brittle** | Water + Frost | Soaked targets hit by frost jump straight to 3–5 counters; **water zones freeze into slippery ice** (low friction, momentum slides) | Gang Beasts sliding |
| **Mudslide** | Water + Earth | Mud patch: heavy slow and **no dashing** | Area denial |
| **Magnetize** | Lightning + Earth | Rubble and debris become charged and shock anyone nearby | Arena gets spicy |
| **Blight Bloom** | Poison + Nature | Poisoned vines grow spore pods that burst into more poison | Blightward and Verdant zone control |
| **Overgrowth Surge** | Water + Nature | Nature zones and healing totems grow bigger and heal more | The support combo |
| **Echo** | Void Mark + any reaction | The reaction **repeats** on the marked target, for 1.5× damage | The Hollow as combo amplifier |

Environment counts too:
- Lava plus water makes a steam burst, then a cooled rock platform.
- Fire melts ice walls faster.
- Map water (Drowned Sanctum) soaks you.
- The ice floor (Frostgrave) is always Brittle-slippery near Frostwarden effects.
- Explosions shove everything.

## 3. Feedback (every reaction must *feel* like a combo)

- A big word pops at the spot in the mix of both element colors: **CONDUCT!**, **SHATTER!**, **COMBUST!**
- Rumble for the triggerer (and a heavier one for victims), a short camera shake and a reaction-specific effect.
- A per-player **combo counter**. In Zombies it pays out points; in brawls it feeds announcer callouts ("TRIPLE REACTION!").
- Reaction kills get a unique kill-feed icon.

## 4. Team roles (TDM and Zombies)

| Role | Wizards | Job |
|---|---|---|
| **Setup** | Tidebound (Soak), Frostwarden (Chill), Blightward (Poison clouds) | Coat enemies and areas in statuses |
| **Detonator** | Voltborn (Conduct), Emberguard (Steam, Combust, Thermal), Granite Vow (Shatter, Mud) | Cash statuses in for burst |
| **Amplifier / Support** | The Hollow (Echo), Verdant Circle (Overgrowth Surge, healing) | Multiply or sustain the combo |

Named comps to advertise on the wizard-select screen:
- **Storm Front:** Tidebound + Voltborn.
- **Frostbreaker:** Frostwarden + Granite Vow.
- **Pyre:** Blightward + Emberguard.
- **Monsoon Grove:** Tidebound + Verdant Circle.
- **Void Echo:** The Hollow + anyone.

In TDM, reactions still hurt everyone they touch, so friendly fire from reactions is optional (toggle). Ally-helpful reactions (Overgrowth Surge, Steam cover) help your team.

## 5. Implementation plan (suggested order)

1. **Element model.** Add an `Element` enum and element statuses to `StatusEffects` (Soaked, Charged, Poisoned and Staggered are new; Burning = brands, Chilled/Frozen = freeze counters, Rooted = root, Marked = void mark).
2. **`ElementReactions` static class.** `OnElementHit(target, attacker, element, damage, point)` checks the target's statuses, triggers at most one reaction per hit, consumes statuses, and raises `DamageEvents` with the attacker as credit. It is data-driven (a table of recipe, effect and tunables) so new reactions are cheap.
3. **Hook sources.**
   - `Bullet.HandleHit`: element from `BulletFX.FlavorOf(wizard.passive)`.
   - Abilities: pass their element when they damage or apply status.
   - `GroundHazard` / poison clouds / lava get an element so zones can react (Combust, Brittle ice, electrified water).
4. **First four reactions:** Conduct, Steam, Shatter, Combust (biggest fun per effort). Then Thermal Shock and Brittle, then the rest.
5. **Feedback:** reaction popup word (world-space text or glow sprite), rumble and shake, then the combo counter.
6. **Tuning pass:** keep reaction damage roughly in the 15–35 range (health is 120 per life). Reactions should feel strong but not delete people alone.

Steps 1–5 are done: every reaction in §2, Echo and the combo counter. Step 6 (tuning) needs playtests.

## 6. Implementation (built 2026-09-30)

### Code map

| Piece | Where |
|---|---|
| `Element`, `ElementStatus` enums; `Elements.Of(wizard/caster)`, `Elements.ColorOf` | `Assets/Scripts/Combat/Element.cs` |
| Statuses (Soaked, Charged, Poisoned, Staggered, Burning timer, Frozen window), `Has` / `Consume`, bullet buildup | `Assets/Scripts/Combat/StatusEffects.cs` |
| Recipe table, source entry points, the four reactions | `Assets/Scripts/Combat/ElementReactions.cs` |
| Zone registry (`IElementZone`, `IElectrifiable`) | `Assets/Scripts/Combat/ElementZones.cs` |
| Popup word | `Assets/Scripts/Combat/ReactionPopup.cs` |
| On-body status hints (drips, crackle, ooze, embers, dust) | `Assets/Scripts/Combat/ElementStatusFx.cs` |
| Steam cloud | `Assets/Scripts/Abilities/Runes/SteamCloud.cs` |
| Rumble presets `ReactionTrigger` / `ReactionVictim` | `Assets/Scripts/Combat/Rumble.cs` |

A wizard's element comes from `BulletFX.FlavorOf(wizard.passive)` (now public), so it follows the faction, not the passive rune.

### How statuses get applied

| Status | Counts as | Applied by |
|---|---|---|
| Burning | **2+ brands** (anyone's, combined) **or** a burn timer | Emberguard bullets (brands, via the passive); fireball, Blazing Ruin dash, brand ignite (3s); standing in lava or a fire patch (1.5s) |
| Chilled / Frozen | 1+ freeze counters / 5 counters, or a freeze window | Frostwarden bullets (counters); the Fractalshot full freeze and Flash Freeze encase open a **Frozen window** for their duration |
| Soaked | 5s | **Tidebound bullets: 35 damage adds up to Soaked** (buildup empties 3s after the last hit); Riptide; Undertow (and any water zone) while inside |
| Charged | 3s | **Voltborn bullets: 30 damage adds up to Charged**; Chain Surge, Stormrunner fences, Blinkstorm pulse, Lightning Reflex bolt |
| Staggered | 1s | Seismic Judgement slam, Rockslide, heavy bullets (30+ dmg) from Granite Vow, any heavy ability knockback |
| Rooted | while rooted | Thornsnare and other roots (fire burns them: Wildfire) |
| Poisoned | while inside, then 3s | Poison clouds (mines, Virulent Shroud), toxic puddles, Plague and Contagion ticks |
| Marked | until its Echo is used | Soulfracture (players and monsters). Echo spends the mark's echo, **not** the curse, so The Hollow keeps the death burst |

Statuses (not brands, freeze counters or void marks, which keep their old rules) clear when you lose a life.

### Sources

- **Bullets** (`Bullet.HandleHit` → `ElementReactions.BulletHit`): react first; if nothing went off, build the shooter's status. A single round of 30+ damage is **heavy**. **Fire rounds that fly through a poison cloud Combust it** (`OnElementPass`, once per bullet).
- **Abilities** (`AbilityHit`): the caster's element; react first, otherwise leave the full status. Hooked: Blazing Ruin (dash hits and lava trail), Fireball (hits and blast area), Brand ignite, Chain Surge, Stormrunner fence, Blinkstorm, Lightning Reflex, Riptide, Seismic Judgement (heavy), Winter's Wind (heavy), Flash Freeze and Fractalshot (Frozen window), Plague, Contagion. Unused alternates (Rockslide, Tidal Surge, Plague Mortar) are hooked too. Void and Nature abilities apply their existing mark and root; no reaction uses those yet.
- **Zones** (`ZoneHit` on each tick, plus the `ElementZones` registry): `GroundHazard` takes its owner's element unless one is set, so Undertow is water, the fireball's lava pool is fire, toxic puddles are poison, and Static Field is lightning. `PoisonCloudHazard` and `LavaTrail` are poison and fire zones. When a zone appears it reacts with zones it touches (a mine's cloud landing on lava, lava laid through a cloud, a static field on a whirlpool); the newcomer gets the credit.

### Reaction rules

- **At most one reaction per hit**, checked in table order: Shatter, Conduct, Combust, Thermal Shock, Brittle, Mudslide, Magnetize, Wildfire, Blight Bloom, Overgrowth Surge, Steam.
- The status that matched is **consumed**, and that target can't react again for **1s**.
- **No matter who applied the status**: it's all free-for-all. Credit (damage, kills, passives, rumble) goes to **whoever triggered it**, through `DamageEvents.Deal`.
- **You're never hurt by your own reaction** (Combust still shoves you). Everyone else, teammates included later, is fair game.
- `ElementReactions.Reacted(reaction, triggerer, target, point)` fires for every reaction: the hook for the combo counter, announcer and kill-feed icon.

| Reaction | What it does in-game |
|---|---|
| **Shatter** | Frozen + an Earth hit or any heavy knockback (sniper round, Winter's Wind, Seismic Judgement). Bonus damage by counters, the ice breaks, the stun ends and they're knocked away from whoever broke it. |
| **Conduct** | Soaked + lightning, or Charged + water. Bolts hop target to target through every Soaked combatant within 8m (damage and a short stun each, never shortening a longer stun), and every water zone in range is electrified for 3s, shocking anyone in it (its owner too, just not the triggerer). A lightning burst or static field landing on a water zone electrifies it. |
| **Combust** | Poisoned + fire, Burning + poison, or fire touching a cloud or puddle. The gas explodes: area damage, knockback, and mines and grenades nearby go off. It leaves a burning patch, which is a fire zone and can set off the next cloud. |
| **Steam** | Soaked + fire, or Burning + water. Scald damage and a thick 3s cloud that hides whoever's inside. Tracers still draw over it, and it scalds everyone in it except the triggerer. |
| **Thermal Shock** | Chilled (2+ counters) or Frozen + fire, or Burning + frost. Cashes in the freeze counters as burst damage (Frozen counts as 5) and clears the brands and ice together. |
| **Brittle** | Soaked + frost, or Chilled + water. Tops the target up to 4 freeze counters (one more Frostwarden hit freezes them). Water zones nearby, or any a frost round flies over, **freeze into slippery ice** for 4s: everyone on it, the one who froze it included, slides (momentum carries, knockback travels further). |
| **Mudslide** | Soaked + earth, or Staggered + water. A 4s mud patch: heavy slow and **no dashing** for enemies. Seismic Judgement or a Granite Vow round over a water zone churns it to mud. |
| **Wildfire** | Rooted + fire, or Burning + nature. Burns the vines off (it ends the root) and leaves a spreading burn zone. Brambles and thickets it touches catch fire, **burning their Verdant owner too**, and fire rounds over growth ignite it. |
| **Magnetize** | Staggered + lightning, or Charged + earth. Four chunks of **charged rubble** tear up and land around the target for 5s. Each arcs at anyone within 2.2m (never the triggerer): a lightning hit that Charges them, and can set off Conduct on the Soaked. Rubble landing in water electrifies it. |
| **Blight Bloom** | Poisoned + nature, or Rooted + poison. Three **spore pods** sprout around the target, swell for 1s and burst into poison puddles, which Combust if fire finds them. |
| **Overgrowth Surge** | Soaked + nature, or Rooted + water. Vines surge up and **root** the target for 1.2s, and the triggerer heals 12. Growth zones within 6m swell once (×1.4 size and damage, +3s) and healing totems heal ×1.5 for 5s. The support combo. |
| **Echo** | Any reaction on a Void-marked target repeats on them 0.35s later at **1.5× damage**, with a void burst. Popup: "ECHO STEAM!". |

**Zones meeting zones** is one small table (`ElementReactions.ZoneRules`): fire + gas = Combust, fire + growth = Wildfire, lightning + water = electrified, frost + water = ice, earth + water = mud, poison + growth = Blight Bloom, water + growth = Overgrowth Surge, lightning + mud = Magnetize, water + lava = a steam burst that cools the lava to rock. Each zone can react at most once every 1.5s (`ZoneCooldown`). Blight Bloom and Magnetize (which spawn new zones) fire at most once per zone so they can't feed themselves, and they, like lava + water, don't trigger from bullets passing over (so Tidebound can't erase a lava trail by shooting across it). The same rules apply to bullets flying over a zone, blasts landing on one (Fireball, Brand ignite, Blinkstorm, Flash Freeze, Seismic Judgement) and zones spawning or changing on top of each other. A zone that changes (water froze, brambles caught fire) reacts again as its new element, so fires spread through growth and ice spreads across connected water.

### Combo counter

`ReactionCombo`: reactions you trigger within **4s** of each other chain. At 2+ a callout pops over your head in your wizard's colors (DOUBLE REACTION!, TRIPLE, QUAD, then ELEMENTAL OVERLOAD!), with rumble that grows and camera shake from 3. Echoes and chain reactions (a Combust setting off the next cloud) count. `ComboChanged(player, count)` and `ComboEnded(player, count)` are the hooks for Zombies points and the announcer.

### Feedback

- **Popup word** (`ReactionPopup`): a world-space bold word that punches in, rises and fades. It's shaded across the word from one element's color to the other's (colors come from the wizards' theme colors) with a dark outline. Multi-target reactions add "x3". It adds nothing to the HUD.
- **Rumble**: a snappy pulse for the triggerer, a heavy one for each victim (on top of the normal hit rumble).
- **Camera shake** per reaction (Combust biggest, Steam smallest), plus a flash and double shockwave ring in both colors.
- **Effects**: dual-color bolts (Conduct), ice shards and mist (Shatter), fireball, embers and toxic smoke (Combust), boiling burst and steam cloud (Steam), all built on GlowLine, AbilityKit, PowerFx and BulletFX motes.
- **Status hints** on bodies: Soaked drips, Charged crackles, Poisoned oozes, Burning sheds embers, Staggered kicks up dust. All small, no HUD.

### Movement hooks added

`PlayerMovement3D` gained named **traction** modifiers (`SetTraction` / `ClearTraction`; the slipperiest wins) and named **dash blocks** (`SetDashBlocked`, `CanDash`), used by ice and mud zones (`GroundHazard.traction`, `GroundHazard.blocksDash`). At traction 1, movement is exactly as before.

### Environment

- **Lava + water**: a water zone meeting a fire zone (Undertow cast on lava, lava laid through a whirlpool) makes a Steam burst and the lava cools to rock (`LavaTrail` / fire `GroundHazard` are used up).
- **Fire melts ice walls** faster. Ice walls (2026-10-01) can't be broken: they erupt from the floor (camera shake, rumble, frost ring, shoving anyone in the footprint out), stand **9s** while slumping to 40% height, then sink away. Other hits only jolt them; each fire round takes `StructureDamage × 0.01`s off the melt (`IceWallEffect.fireMeltPerDamage`). Size is ×1.8 wide, ×2 tall, ×1.3 thick vs. the old wall (`sizeScale`); max 2 up per caster.
- **Explosions shove everything** caught in them, the one who set it off included: 0.35 × blast damage at the center (max 14), 35% at the edge (`Explosions.ShovePerDamage`, `MaxShove`). Seismic Judgement opts out (it has its own ground-only shove).
- **Map water and lava**: `MapElementZone` on a collider makes map terrain an element zone. Water Soaks anyone standing in it and can be electrified, frozen to ice or churned to mud (temporarily; it's never used up). Lava sets people Burning and turns Undertow to steam. **It still has to be placed in the editor** (see below).

### Needs the editor

- Add `MapElementZone` (Water) to Drowned Sanctum's walkable shallows and (Fire) to Cinder Crucible's lava. **Don't put it on the big planes as they are.** Drowned Sanctum's `Water` is a 73×65 plane and Cinder Crucible's `GroundLava` a 58×60 plane at y ≈ −1, right under the arena floor. With `standHeight` 1.2, players standing on the floor above would count as inside, so everyone would be Soaked or Burning all the time. Give the component (via its `area` field) its own box collider covering only where players actually wade or step in lava. (Non-convex plane colliders work, measured by their bounds.)
- Frostgrave's `FrozenLake`: to make it "always Brittle-slippery near Frostwarden effects", give it a Water `MapElementZone` with `appliesStatus` off (so it doesn't Soak). Frost that reaches it freezes it into slippery ice for 4s.

### Sound

`ReactionAudio` plays one sound per reaction from `Assets/Resources/ReactionSounds.asset` (a `ReactionSoundBank`; swap clips there, no code). It's 2D (one shared screen), with a little pitch jitter, and long clips are cut to `maxLength`. Echo replays the reaction's sound deeper. Clips come from the gun pack's Bonus_Sounds: taser zaps (Conduct, Magnetize), explosions (Combust large, Thermal Shock small), a fuse burn as the hiss (Steam, Wildfire), body impacts (Shatter), wet impacts (Mudslide, Blight Bloom, Overgrowth) and static (Brittle). The announcer says "Frenzy" when a combo reaches 4 and "Killing spree" at 5. **Placeholders**: there are no ice, steam or splash sounds in the project yet.

### Reaction kills

Damage a reaction deals, including what it leaves behind (steam scald, burning patches, poison puddles, electrified water, charged rubble), goes through `ElementReactions.DealAs`. A kill it lands fires `ReactionKilled(reaction, killer, victim, where)`, the hook for a future kill-feed icon, and pops a big "COMBUST KILL!" in the reaction's colors.

### Team roles and comps on wizard select

`ElementComps` holds §4's roles and named comps. The wizard select lore box now starts with the highlighted wizard's role and comps ("**Setup** · Storm Front (+Voltborn) · Monsoon Grove (+Verdant Circle)"). In co-op/team modes (`SelectedMode` isn't deathmatch), if someone who already picked completes a comp, it calls it out in gold: "**STORM FRONT** with P1's Voltborn! Soak them, then Conduct."

### Seed of Aloria totem

The healing totem is a nature zone (its heal range). Water surges it (×1.5 healing for 5s), poison blooms spore pods around it once, and fire sets it ablaze (Wildfire): **no healing for 4s** and a fire zone around it that hurts everyone, its Verdant owner included. It can't be used up; it still breaks only when its crystal does.

### Teams and friendly fire

`Teams`: `TeamOf(player)` (−1 = free-for-all) and `SameTeam`. There's no TDM yet, so deathmatch is everyone-for-themselves and Zombies puts all players on one team. `Teams.ReactionFriendlyFire` (on by default, per the design) is enforced for all reaction damage. `Teams.BulletFriendlyFire` (off) is declared but bullets don't check teams yet; that belongs with TDM.

### Zombies points

`ZombiesPoints` (active on maps with a `GoblinSpawner`): 10 per hit on a monster, 60 per kill, **30 per reaction**, and a finished combo pays 25 × its length. Big earns pop a small "+60" over the player. `TrySpend` is ready for wall buys, and `Multiplier` for Double Points. There's no HUD total yet; add it with wall buys.

### Not built yet

The kill feed itself (hooked via `ReactionKilled`); a HUD points total and wall buys; TDM (team assignment, team colors, bullet friendly fire); real ice/steam/splash sounds.
