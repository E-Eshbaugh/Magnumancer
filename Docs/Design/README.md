# Magnumancer Design Docs

Living design notes for Magnumancer. Start here, then open the doc for the system you're building.

## Vision

A fun, chaotic, combo-forward **couch co-op brawler**: *CoD Zombies meets Gang Beasts meets Super Smash Bros meets Brawl Stars.*

- 2–4 players on one screen, controllers only.
- Wizards with guns. Each wizard is an element with a signature ability, a passive and weapon affinities.
- Fights should feel **rewarding, chaotic and exciting**. Knockback, combos and flashy effects beat precision and patience.
- A **match** on one map lasts roughly **5–10 minutes**. Later, matches chain into a **brawl campaign**: first to 3, 5 or 7 match wins across maps.
- Modes: **Deathmatch** (current focus), then **Team Deathmatch** and **Zombies** (waves, co-op).

### Design pillars

1. **Everything interacts.** Elements react with each other no matter who applied them, in every mode, even free-for-all. See [Elemental Ecosystem](elemental-ecosystem.md).
2. **Readable chaos.** Each player's bullets, trails and effects are in their wizard's color. Big moments get big feedback (flash, shake, rumble, slow-mo), and the HUD stays out of the way.
3. **Builds matter.** Wizard choice, runes, loadout orbs and weapon weight all trade against each other.
4. **Always something to do.** Short dash cooldowns, kill rewards, item drops and fast respawns. Nobody sits out long.
5. **Anti-snowball.** Comebacks should be possible: crowns/bounties on the leader, and losers pick first in drafts.

## Docs

| Doc | What's in it | Status |
|---|---|---|
| [Elemental Ecosystem](elemental-ecosystem.md) | Element statuses, reactions, team roles, implementation plan | **Built**: statuses, 8 reactions + Echo, combo counter; needs playtest. Magnetize, Blight Bloom, Overgrowth Surge next |
| [Items & Drops](items-and-drops.md) | Map pickups, wonder weapons, zombies economy | Designed |
| [Modes & Match Flow](modes-and-flow.md) | Brawl campaign, sudden death, drafts, hazards, TDM, Zombies | Designed |
| [Balance Log](balance-log.md) | What's been tuned, where the knobs live, open issues | Up to date (2026-09-30) |

## Codebase map (systems these designs build on)

| System | Where | Notes |
|---|---|---|
| Damage and kill events | `Assets/Scripts/Combat/DamageEvents.cs` | `Damaged` / `Killed` events, incoming/outgoing modifiers |
| Status effects | `Assets/Scripts/Combat/StatusEffects.cs` | Brands (fire), freeze counters, stun/root, void mark, plus element statuses (Soaked, Charged, Poisoned, Staggered, Burning/Frozen windows) with `Has` / `Consume` |
| Elements and reactions | `Assets/Scripts/Combat/Element.cs`, `ElementReactions.cs`, `ElementZones.cs` | Element model, data-driven recipe table, `BulletHit` / `AbilityHit` / `ZoneHit` sources, zone registry, `Reacted` event |
| Reaction feedback | `Assets/Scripts/Combat/ReactionPopup.cs`, `ElementStatusFx.cs`, `ReactionCombo.cs`, `Abilities/Runes/SteamCloud.cs` | Popup word, on-body status hints, combo callouts, steam cloud |
| Stuns | `StunEffect` via `StatusEffects.Stun/Root` | Stunned players can't jump (`PlayerMovement3D.SetJumpBlocked`) |
| Bullets | `Assets/Scripts/Player/Weapon/Bullet.cs`, `BulletFX.cs` | Hit handling, knockback, element hits; `BulletFX.FlavorOf(passive)` maps each wizard to an element; `BulletFX.Mote` emits element bits for other effects |
| Glow visuals | `Assets/Scripts/Player/GlowLine.cs`, `Assets/Scripts/Abilities/Runes/AbilityKit.cs` | Additive lines, rings, zaps, glow orbs, shockwaves |
| Ground zones | `Assets/Scripts/Abilities/Runes/GroundHazard.cs` | Damage/slow circles (poison puddles, lava, ice). Carry an element (the owner's by default) and register as element zones, as do `PoisonCloudHazard` and `LavaTrail` |
| Explosions | `Assets/Scripts/Combat/Explosions.cs` | Shootable mines/grenades, area damage |
| Wizards and runes | `Assets/Resources/Wizards/*.asset`, `Assets/Scripts/Data/RuneBook.cs` | 3 active runes, 2 passive runes, 1 weapon affinity per wizard |
| Weapon affinity | `Assets/Scripts/Combat/WeaponSynergy.cs` | Per-wizard bonus with favoured weapon classes |
| Weapons | `Assets/Resources/Weapons/*/*.asset` | Tiers: Initiate, Ascendant, Archon |
| Movement, weight, knockback | `Assets/Scripts/Player/PlayerMovement3D.cs` | Weight table, `ApplyKnockback`, `AddKnockback` (stacking), `IsAirborne`, traction (ice) and dash blocks (mud) |
| Health, lives, death | `Assets/Scripts/Player/PlayerHealthControl.cs`, `WizardDeathEffect.cs`, `WizardSpawnEffect.cs` | Fireball death, bolt spawn, HUD grey-out |
| Match end | `Assets/Scripts/MapControl/WinManager.cs` | Last player standing, then MainMenu (no rounds yet) |
| Player setup | `Assets/Scripts/MapControl/MultiplayerManager.cs` | Wires wizard, loadout, passives, bars |
| Rumble and shake | `Assets/Scripts/Combat/Rumble.cs`, `CameraShake` | |

Known intentional quirks (don't "fix"): Akimbo's off-hand fires backwards; the map select's slot 4 loads CinderCrucibleZombies; players are immune to their own mines and poison.
