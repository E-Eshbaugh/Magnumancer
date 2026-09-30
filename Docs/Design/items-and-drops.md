# Items & Drops

> Smash items + Brawl Stars power-ups + CoD Zombies wonder weapons. Drops create **hot spots** that pull everyone into the same fight.

## Spawning

- A drop lands every **30–45s** (scaling with player count) at a map spawn point, **telegraphed ~2s ahead** by a beam of light from the sky. It reuses the spawn-bolt look in white or gold, with an announcer sting.
- Pick up by walking over it. Its glow and icon are readable from anywhere on screen.
- Rarity weights per mode; the common/rare split is tuned so a rare drop is an event.
- Map-specific pools are possible later (Frostgrave drops more frost items, etc.).

## Brawl pickups (Deathmatch / TDM)

| Item | Rarity | Effect |
|---|---|---|
| **Rune Shard** | Common | Ability cooldown refreshed instantly |
| **Healing Draught** | Common | +40 HP (up to max) |
| **Overdrive Orb** | Uncommon | 8s: +50% fire rate, no reloading |
| **Elemental Rounds** | Uncommon | Next magazine carries a **random other element** (feeds the reaction ecosystem, great chaos) |
| **Blink Charm** | Uncommon | 3 instant dash charges |
| **Aegis Sigil** | Uncommon | 50-point shield for 6s |
| **Throwables** | Common | Hex grenade (random element burst), sticky goblin bomb, portal stone (drop a 5s two-way portal) |
| **Wonder Weapon** | Rare | Replaces your held slot until its ammo runs out (below) |
| **Heart Relic** | Very rare / long modes only | +1 life |

### Wonder weapons (limited ammo, huge personality)

- **Singularity Launcher:** black-hole grenade that pulls everyone in, then pops.
- **Frost Cannon:** freezes a whole cone solid (perfect Shatter setup).
- **Thunder Maul:** short-range blast that launches people across the map (the Smash hammer).
- **Gale Horn:** a huge shove cone, excellent near hazards and ledges.
- **Ember Minigun:** every bullet brands; spins up, melts down.

## Leader crown (anti-snowball)

- The current leader (most kills, or most lives left) wears a **glowing crown**, visible to everyone.
- Killing the crown holder grants a bonus (a Rune Shard plus an instant reload), and the crown passes on.

## Zombies economy (co-op)

> *Built (2026-09-30):* earning points (`ZombiesPoints`: hits, kills, reactions, combos). Spending (wall buys) and a HUD total are next.

- **Points:** hits, kills, and **reactions (bonus!)** earn points, so the combo counter directly pays.
- **Mystery Box:** random roll, including wonder weapons.
- **Wall buys:** weapons chalked on walls, bought with points.
- **Perk Shrines** (elemental perks):
  - *Stormheart* (dash leaves sparks).
  - *Tidal Blood* (reloads soak nearby).
  - *Ironhide* (knockback immune).
  - *Phoenix Rite* (self-revive once).
  - *Quick Sigil* (faster ability cooldown).
- **Enchanting Altar** (Pack-a-Punch): upgrades a gun to an elemental version with a new name, a bigger magazine and the element on every hit.
- **Power-up drops from zombies:** Max Ammo, Arcane Surge (abilities refreshed), Nuke (Meteor Fall), Double Points, Hex Bullets.
- **Downed and revive:** a teammate holds a button to revive; bleed-out timer.

## Implementation notes

- A `PickupSpawner` per map with spawn points (empty transforms) and a weighted item table (ScriptableObjects: `ItemData` with icon, color, rarity and effect prefab/type).
- `Pickup` component: trigger collider, bob and spin, glow; on touch calls `IPickupEffect.Apply(player)`.
- Timed buffs reuse existing modifier hooks: `AmmoControl.SetFireRateModifier`, `PlayerMovement3D.SetSpeedModifier`, `IIncomingDamageModifier` for shields.
- The telegraph beam reuses `WizardSpawnEffect`'s bolt code or `GlowLine`.
