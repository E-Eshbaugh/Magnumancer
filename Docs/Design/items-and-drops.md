# Items & Drops

> Smash items + Brawl Stars power-ups + CoD Zombies wonder weapons. Drops create **hot spots** that pull everyone into the same fight.

> *Built (2026-10-01):* drop spawning, all brawl pickups, all five wonder weapons, the leader crown. Code in `Assets/Scripts/Items/`; see **As built** below. Needs playtest. The Zombies economy is still design-only (earning points is built).

## Spawning

- A drop lands every **8–14s** (scaling with player count) at a map spawn point, **telegraphed ~2s ahead** by a beam of light from the sky. It reuses the spawn-bolt look in white or gold, with an announcer sting.
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

## As built (2026-10-01)

### How it plays

- **First drop at 6s**, then every **14s with 2 players → 8s with 4** (±12%): about 55 drops in a 7.5-minute 4-player match. A beam marks the spot for **2s** (white, or gold plus a "RARE DROP!" call for rare drops), then a bolt strikes and the item appears.
- Spots are picked **each time around the living players**: open, flat floor near the middle of the fight, on screen, at least 2.5m from everyone and 4m from other drops. (Some maps scroll, so fixed points could be off camera.) A map can override this with empty objects named `DropPoint...`.
- **At most 4 items on the field**: a new drop removes the oldest. An untouched item blinks after 15s and vanishes at 20s.
- Pickups: a big (×2.2, rare ×2.6) model per item: art from the project's packs (gems, KayKit bottle/shield/keg/staff/bomb, Styloo ammo box/bullets/grenade/rocket launcher, Kenney blaster, Low Poly M249), refitted, recolored in the item's color with a little glow and dressed with glowing details, plus a made-in-code war horn (Gale Horn) and heart (Heart Relic). Models are slots on `Resources/ItemModels.asset` (`ItemModelBank`); the build per item is `ItemVisuals.BuildModel` bobbing over a dark shadow, a ring in its color (an extra gold ring if rare) and a small light. No overhead text. Walk over it to take it: the item leaps over your head, spins with wheeling light rays, dives into you and bursts (`ItemGetFx`), while its name (or what it did, e.g. "+40", "FROST ROUNDS!") slams in over you with bouncing, waving, shimmering letters (`ItemGetText`). Rare items get gold rays and a second gold shockwave.
- Drops stop once the match is down to its last player (solo testing keeps them coming).
- **Controls:** **LB throws** your held throwable (a small copy floats over your shoulder). A **wonder weapon takes RT** and replaces your gun until its ammo runs out or you lose a life. Your own gun keeps its magazine and comes back, and its LT ability waits meanwhile.

### Items

| Item | What it does (as built) |
|---|---|
| Rune Shard | Ability ready now |
| Healing Draught | +40 HP |
| Overdrive Orb | 8s: ×1.5 fire rate on every gun you carry, shots cost no ammo (no reloads) |
| Elemental Rounds | Refills your held gun with a magazine of a random element that isn't yours: **Fire, Frost, Water, Lightning, Earth or Poison** (bullet-friendly statuses; Nature roots and Void marks stay ability-only). Rounds look like that element and leave its status: fire brands, frost chills (one counter per volley), water/lightning build Soaked/Charged, earth staggers, poison poisons. Ends when the magazine is spent, or you reload, swap or lose a life |
| Blink Charm | +3 dashes with no cooldown (pips circle your waist; stacks to 6, kept until used) |
| Aegis Sigil | 50-point shield for 6s (faint bubble, shatters when spent) |
| Hex Grenade | Lobbed 9m; 3.2m burst of a **random element** (any of the 8), 22 dmg; leaves that element's status or sets off a reaction |
| Goblin Bomb | Sticks to the first enemy it touches (or the floor), fizzes 1.5s, then 50 dmg / 3.8m blast (heavy: Shatters the Frozen). If the wearer loses a life it drops where they fell |
| Portal Stone | Portal at your feet + one where it lands, linked both ways for 5s, anyone can use them (reuses Void Rift's portals) |
| Singularity Launcher | 3 shots. Lobbed black hole: pulls enemies in for 1.6s (7m), then pops: 40 void dmg in 4m, heavy, shoves out |
| Frost Cannon | 4 shots. 9m / 60° cone: 15 dmg and **frozen solid for 2.2s** (encased in ice, Shatter-able) |
| Thunder Maul | 5 swings. 3.6m / 150° slam: 28 lightning dmg (heavy) and a launch of 85 (the Smash hammer) |
| Gale Horn | 4 blasts. 12m / 65° gust: shove of 60, 4 dmg, heavy (Staggers, Shatters the Frozen) |
| Ember Minigun | 150 rounds. Hold RT to spin up (0.5s), 18 rounds/s of 6-dmg **fire rounds that brand**, you move at 75% while holding it. When empty it **melts down** into a lava pool at your feet (it can't hurt you) |
| Heart Relic | +1 life. Only drops after the match has run **3 minutes** |

Drop weights (out of ~92): Rune Shard 14, Healing Draught 14, Hex 6, Goblin Bomb 6, Portal 5, Overdrive 9, Elemental Rounds 9, Blink 8, Aegis 9, each wonder weapon 1.8 (~10% that a drop is a wonder weapon: about 5 per 4-player match), Heart Relic 2. The same item is rerolled once if it would land twice in a row.

### Leader crown (built)

- Leader = **most kills** (taking a life from another player), then **most lives left**. Nobody wears it while everyone's even; a tie keeps it where it is.
- A gold crown floats over the leader. **Taking a life from the crown holder** makes your ability ready, refills your magazine and pops "CROWN BREAKER!". The crown then goes to whoever leads (the breaker wins ties).

### Code map

| Piece | Where |
|---|---|
| Item table (names, rarity, colors, weights) | `Items/ItemBook.cs`. Kept in code like RuneBook, not ScriptableObjects; a per-mode table can plug into `ItemBook.Roll` |
| Timing, placement, bootstrap, crown | `Items/DropDirector.cs` (added automatically to match scenes with a `MultiplayerManager` and no `GoblinSpawner`; `DropDirector.Enabled` switches it off), `Items/LeaderCrown.cs` |
| Beam, pickup, models, labels | `Items/DropBeam.cs`, `Items/Pickup.cs`, `Items/ItemVisuals.cs` |
| Effects | `Items/ItemEffects.cs` (instant items + tuning statics), `Items/Buffs/*`, `Items/Throwables/*`, `Items/Wonder/*` |
| Sounds | Empty slots on `Resources/ReactionSounds.asset` → "Item drops" (incoming, land, rare announcer, pickup, crown taken). Until they're filled, the strike borrows the Conduct crackle and the rest is silent |
| Hooks added to existing code | `AmmoControl.SetFiringBlocked` / `SetFreeAmmo` / `ShotElement`; `FireController3D.Shoot(..., element)`; `Bullet.element`; `BulletFX.StyleOf(Element)`; `ElementReactions.BulletHit(..., infused)`; `PlayerHealthControl.AddLife` / `LivesLeft`; LT gun abilities wait while `FiringBlocked` |

**Testing in the editor:** keys **1-9** drop items 1-9 (ItemBook order) next to player 1, **Shift+1-6** items 10-15, **0** sends a random drop with its beam.
