# Elemental Ecosystem

> **Status (2026-09-30):** the core is **built**: the element model, all statuses, the reaction system, element sources (bullets, abilities, zones) and the first four reactions (**Conduct, Steam, Shatter, Combust**) with feedback. Not playtested yet. See [§6 Implementation](#6-implementation-built-2026-09-30) for what exists and how it behaves. Numbers are in the [Balance Log](balance-log.md) (Pass 3).

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

Steps 1–4 are done, and step 5 is done except the combo counter (the `ElementReactions.Reacted` event is its hook).

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
| Rooted | while rooted | Thornsnare and other roots (tracked, no reaction uses it yet) |
| Poisoned | while inside, then 3s | Poison clouds (mines, Virulent Shroud), toxic puddles, Plague and Contagion ticks |
| Marked | until used | Soulfracture (tracked, no reaction uses it yet) |

Statuses (not brands, freeze counters or void marks, which keep their old rules) clear when you lose a life.

### Sources

- **Bullets** (`Bullet.HandleHit` → `ElementReactions.BulletHit`): react first; if nothing went off, build the shooter's status. A single round of 30+ damage is **heavy**. **Fire rounds that fly through a poison cloud Combust it** (`OnElementPass`, once per bullet).
- **Abilities** (`AbilityHit`): the caster's element; react first, otherwise leave the full status. Hooked: Blazing Ruin (dash hits and lava trail), Fireball (hits and blast area), Brand ignite, Chain Surge, Stormrunner fence, Blinkstorm, Lightning Reflex, Riptide, Seismic Judgement (heavy), Winter's Wind (heavy), Flash Freeze and Fractalshot (Frozen window), Plague, Contagion. Unused alternates (Rockslide, Tidal Surge, Plague Mortar) are hooked too. Void and Nature abilities apply their existing mark and root; no reaction uses those yet.
- **Zones** (`ZoneHit` on each tick, plus the `ElementZones` registry): `GroundHazard` takes its owner's element unless one is set, so Undertow is water, the fireball's lava pool is fire, toxic puddles are poison, and Static Field is lightning. `PoisonCloudHazard` and `LavaTrail` are poison and fire zones. When a zone appears it reacts with zones it touches (a mine's cloud landing on lava, lava laid through a cloud, a static field on a whirlpool); the newcomer gets the credit.

### Reaction rules

- **At most one reaction per hit**, checked in table order: Shatter, Conduct, Combust, Steam.
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

### Feedback

- **Popup word** (`ReactionPopup`): a world-space bold word that punches in, rises and fades. It's shaded across the word from one element's color to the other's (colors come from the wizards' theme colors) with a dark outline. Multi-target reactions add "x3". It adds nothing to the HUD.
- **Rumble**: a snappy pulse for the triggerer, a heavy one for each victim (on top of the normal hit rumble).
- **Camera shake** per reaction (Combust biggest, Steam smallest), plus a flash and double shockwave ring in both colors.
- **Effects**: dual-color bolts (Conduct), ice shards and mist (Shatter), fireball, embers and toxic smoke (Combust), boiling burst and steam cloud (Steam), all built on GlowLine, AbilityKit, PowerFx and BulletFX motes.
- **Status hints** on bodies: Soaked drips, Charged crackles, Poisoned oozes, Burning sheds embers, Staggered kicks up dust. All small, no HUD.

### Not built yet

Thermal Shock, Brittle, then the rest of §2; Echo; environment reactions (map water, lava + water); the combo counter and announcer; reaction kill-feed icons; the TDM friendly-fire toggle (there are no teams yet); a sound per reaction (no audio assets hooked up).
