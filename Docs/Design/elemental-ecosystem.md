# Elemental Ecosystem

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
