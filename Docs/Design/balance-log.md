# Balance Log

Numbers change often. This records **what was tuned, why, and where the knobs live**. Update it with every balance pass.

## Pass 1 (2026-09-29)

Lore-matched wizard hearts, loadout orbs, cooldowns and move speed; weapon stats; passive tuning. Budget rule: **more hearts = smaller orb budget**.

## Pass 2 (2026-09-30): couch-brawler pace

Goal: chaotic, combo-forward, ~5–10 min matches.

### Global

| Knob | Old | New | Where |
|---|---|---|---|
| Health per life | 100 | **120** | `Player 1.prefab` → PlayerHealthControl.maxHealth |
| Dash cooldown | 3s | **2s** | `Player 1.prefab` → PlayerMovement3D.dashCooldown |
| Respawn invulnerability | 1s | **2s** | `PlayerHealthControl.respawnInvulnerability` |
| Gunfire knockback | none | **0.18 × damage per hit, stacks, capped at 12** | `Bullet.knockbackPerDamage`, `PlayerMovement3D.maxHitKnockback` |
| Kill reward | none | **Taking a life refills your held magazine** (players only) | `AmmoControl.refillOnKill` |

### Weapon weight (rework)

| Held weight | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---|---|---|---|---|---|
| Move speed | 100% | 97% | 92% | 86% | 78% | 68% |
| Knockback resisted | 0 | 8% | 16% | 24% | 32% | 40% |
| Dash cooldown | +0 | +6% | +12% | +18% | +24% | +30% |
| Draw time after swap | 0.15s | 0.21s | 0.27s | 0.33s | 0.39s | 0.45s |

- **Loadout weight:** total weight over 8 costs 1.5% speed per point (max 15%).
- Stonebind halves speed penalties.
- The weapon select description shows "Heavy (5): 32% slower in hand - loadout weight N".
- Knobs: `PlayerMovement3D` (`heldWeightSpeed`, `packWeightAllowance`, `packSlowPerPoint`, `knockbackResistPerPoint`, `dashCooldownPerPoint`, `weightPenaltyScale`), `AmmoControl.swapDelayPerWeight`.

Weights: Mac110 1 · PumpSG, M1, AK12 2 · AK, M4, SideFed, LeverAction 3 · **SVD 4** (was 3), **GrenadeLauncher 4** (was 3), AutoSG 4 · Minigun 5.

### Weapons

| Weapon | Change | Why |
|---|---|---|
| PumpSG (Scrapshot) | pellet dmg 6→7, spread 20→18 | Weakest gun (53 DPS) |
| Minigun (Iron Tempest) | dmg 5→6 | Pays for the heavier weight penalty |
| M4 (Gravewhisper) | reload 1→1.5s, spread 4→5 | Was strictly the best rifle |

### Wizards

| Wizard | Hearts | Loadout orbs | Ability CD |
|---|---|---|---|
| Blightward | 2→**3** | 10→**9** | 15→**14** |
| Emberguard | 3 | 8 | 12 |
| Frostwarden | 2→**3** | 10→**9** | 12 |
| Granite Vow | 5→**4** | 8 | 16→**15** |
| The Hollow | 2→**3** | 12→**10** | 16→**15** |
| Tidebound | 3 | 8 | 14→**13** |
| Verdant Circle | 4 | 8 | 18→**16** |
| Voltborn | 3 | 8 | 12 |

- Voltborn Lightning Reflex bolt cap 60→**50** (dashes are 50% more frequent now).

### Ability rework: Seismic Judgement (Granite Vow)

- A **0.4s windup** telegraph: an edge ring plus a closing ring; jump when they meet.
- **Airborne players are untouched** by every tick (`PlayerMovement3D.IsAirborne`).
- The slam (first tick) knocks back and stuns grounded players.
- **Stunned, rooted or frozen players can't jump** (`StunEffect` sets `SetJumpBlocked("stun")`), so a mistimed jump means eating the aftershocks.
- Aftershocks (12 per tick) chip grounded players but never stun.

## Pass 3 (2026-09-30): Elemental Ecosystem v1 (new numbers only)

The first four reactions and the new statuses shipped. **No existing number was retuned.** Everything below is new and a first guess, so it needs playtesting. Reaction damage stays in the 15–35 band the design asks for (120 HP per life).

### Reactions (`ElementReactions.Table`, one place)

| Reaction | Recipe | Numbers |
|---|---|---|
| Shatter | Frozen + Earth hit, or any heavy knockback | 10 + 4 per freeze counter (Frozen counts as 5 = **30**), knockback 14, breaks the ice and ends the stun |
| Conduct | Soaked + Lightning, or Charged + Water | **16** to every Soaked target within **8m** (each consumed), 0.6s stun (25% speed). Water zones in range are electrified for **3s** and shock at **10 dps** plus a 0.3s stun |
| Combust | Poisoned/poison cloud + Fire, or Burning + Poison | **26** at the center down to 40% at the edge, radius **3.5** (a cloud blasts its own size + 1.5), knockback 16, sets off mines and grenades, leaves a **3s** burning patch (8 dps) |
| Steam | Soaked + Fire, or Burning + Water | **10** to the target, 3.2m cloud for **3s** that hides whoever's inside, 4 dps scald |

### Globals (`ElementReactions`)

| Knob | Value | Why |
|---|---|---|
| `ReactionCooldown` | 1s per target | Stops a shotgun volley or zone ticks from chaining reactions on one person |
| `HeavyBulletDamage` | 30 | A single round this strong counts as heavy knockback (M1, SVD) and can Shatter |
| `SoakBuildup` | 35 bullet damage | Tidebound: about one close shotgun blast, or 3–4 rifle hits |
| `ChargeBuildup` | 30 bullet damage | Voltborn: Charged is short (3s), so it builds a bit faster |
| `AbilityBurnTime` / `ZoneBurnTime` | 3s / 1.5s | Burning from fire abilities / from standing in lava |
| `MaxDepth` | 4 | Reaction chains stop this deep |

### Statuses (`StatusEffects`, per target)

| Knob | Value |
|---|---|
| `soakDuration` | 5s |
| `chargeDuration` | 3s |
| `poisonLinger` | 3s after the last poison tick |
| `staggerDuration` | 1s |
| `brandsForBurning` | 2 brands (from anyone, combined) count as Burning |
| `buildupDecayTime` | Soak/charge buildup empties 3s after the last hit |
| `frozenSettleTime` | 0.12s: the hit that freezes you can't also shatter you |
| `PoisonCloudHazard.reactionPadding` | 0.6m: clouds react a bit beyond their trigger collider |

### Rules worth knowing when tuning

- **The triggerer is never hurt by their own reaction** (Combust still shoves them). Everyone else is fair game, including in free-for-all.
- Reaction damage goes through `DamageEvents.Deal`, so the triggerer's **outgoing damage modifiers apply** (Inferno Rounds +35% makes a Steam hit harder). Watch for this.
- A reaction consumes **all** brands on the target (everyone's), which can cost an Emberguard their ignite.

## Pass 4 (2026-09-30): more reactions, Echo, combos (new numbers only)

Again, nothing existing was retuned. All numbers live in `ElementReactions.Table` / `ZoneRules` and `ReactionCombo`.

| Reaction | Recipe | Numbers |
|---|---|---|
| Thermal Shock | Chilled 2+ or Frozen + Fire, or Burning + Frost | 4 + **6 per counter** (16 at 2, **34** frozen); needs 2+ counters |
| Brittle | Soaked + Frost, or Chilled + Water | **8**, freeze counters topped up to **4**; water zones within 4m freeze to ice for **4s** at traction **0.12** |
| Mudslide | Soaked + Earth, or Staggered + Water | **6**, 2.8m mud patch for **4s**: enemies 55% slower (×0.45) and can't dash |
| Wildfire | Rooted + Fire, or Burning + Nature | **12**, 2.5m burn zone for **4s** at **8 dps**; ignited growth burns at 8+ dps for 4s |
| Echo | any reaction on a Void-marked target | repeats **0.35s** later at **1.5×** damage (`EchoDelay`, `EchoScale`) |

| Knob | Value | Where |
|---|---|---|
| Combo window | 4s between reactions | `ReactionCombo.Window` |
| Ice feel | acceleration lerps 4→60 u/s² with traction; knockback decay ×0.3 at traction 0 | `PlayerMovement3D` Update / FixedUpdate |

## Pass 5 (2026-09-30): last reactions and environment (new numbers only)

| Reaction | Recipe | Numbers |
|---|---|---|
| Magnetize | Staggered + Lightning, or Charged + Earth | **10**; 4 charged rubble within 3m for **5s**, each arcs every **0.6s** at one target within **2.2m** for **6** |
| Blight Bloom | Poisoned + Nature, or Rooted + Poison | **8**; 3 pods on a 1.8m ring burst after **1s** into 1.8m poison puddles (**6 dps**, 4s) |
| Overgrowth Surge | Soaked + Nature, or Rooted + Water | root **1.2s**, triggerer heals **12**; growth within 6m ×1.4 radius/damage +3s (once each); totems heal ×1.5 for 5s |

| New rule | Value | Where | Note |
|---|---|---|---|
| Explosions shove | 0.35 × damage, max **14**, 35% at the edge | `Explosions.ShovePerDamage`, `MaxShove` | **New behavior** from the design ("explosions shove everything"). Grenades, fireball, brand ignite, Blinkstorm and Combust now shove; mines (0 damage) don't. Set `ShovePerDamage = 0` to turn off. |
| Fire vs ice walls | ×2 damage | `Bullet.FireVsIce` | Fire rounds only |
| Zone reaction cooldown | 1.5s per zone | `ElementReactions.ZoneCooldown` | Stops a bullet stream over one puddle spamming |

## Pass 6 (2026-09-30): Zombies points, reaction sounds (new numbers only)

| Knob | Value | Where |
|---|---|---|
| Points per monster hit | 10 (every pellet counts) | `ZombiesPoints.PerHit` |
| Points per kill | 60 | `ZombiesPoints.PerKill` |
| Points per reaction | 30 | `ZombiesPoints.PerReaction` |
| Combo payout | 25 × combo length when it ends | `ZombiesPoints.PerComboStep` |
| Reaction sound levels | master 0.8, per-reaction 0.5–0.9, announcer 0.9 | `Resources/ReactionSounds.asset` |

## Pass 7 (2026-09-30): destructible environment (new numbers only)

See [Destructible Environment](destructible-environment.md). Knobs: `DestructibleSetup.Rules` (per-prop toughness, explosive barrels), `DestructibleSetup.BaseHealth` (20 + 35 × size) and `MaxHealth` (400), `Destructible.ElementMultiplier`, `PropHealthBar.VisibleFor` (2.5s), `Craters.MinBlastDamage` (26) and `MaxScars` (40).

| Thing | Value |
|---|---|
| Explosive barrels / kegs | 35 damage, 3.5m, full shove, chain |
| Fire vs wood, plants, ice | ×2 |
| Earth vs stone | ×1.5 |
| Explosion damage to props | full at the center, 30% at the edge |

## Pass 8 (2026-09-30): destructibles, second pass (new numbers only)

| Knob | Value | Where |
|---|---|---|
| Walls | toughness ×2.2, stub at 35% height with 40% health | `DestructibleSetup.WallRule`, `Destructible.stubHeight` / `stubHealth` |
| Boundary margin | 3m from the walls' outer edge | `DestructibleSetup.BoundaryMargin` |
| Tall stone stubs | over 2.2m | `DestructibleSetup.TallStub` |
| Tree stumps | 12% height | tree rule `stubHeight` |
| Burning props | 8 dps for 4s, 30%/s spread within 1.5m | `Destructible.BurnDps`, `BurnTime`, `SpreadChance`, `SpreadRadius` |
| Brittle (frost on stone) | ×1.5 for 4s | `Destructible.BrittleMultiplier`, `BrittleTime` |
| Terrain craters | 0.12m per metre of radius, max 0.5m each, 1.2m total | `TerrainCraters` |
| Bullet marks | pool of 220 | `ImpactMarks.Max` |

## Open issues / to playtest

- **Blinkstorm stun is 5s** (`LightningBlastDamage`: `Stun(0.2f, 5f)`). With stuns now also blocking jumps that's very punishing. Suggest **~1.5s**.
- Check that gunfire knockback isn't too floaty or too weak (`knockbackPerDamage` 0.18). Should shotguns get an extra multiplier?
- Pack weight rarely triggers under current orb budgets (most loadouts total 5–12). Consider allowance 6.
- Match length is untested with 120 HP and 3–4 hearts. Add sudden death (see [Modes & Match Flow](modes-and-flow.md)) as the hard cap.
- Ability damage values (Rune II and III actives) haven't had a dedicated pass.
- Blightward (lime) and Verdant Circle (green) bullet colors are the closest pair; tweak a `themeColor` if they're hard to tell apart.
- **Elemental reactions (Pass 3) are unplaytested.** Watch for: Tidebound shotguns turning every Emberguard target into Steam (Water + 2 brands), how often a sniper Shatters (any 30+ damage round on someone Frozen), and whether Combust chains through a Viper's Nest's three clouds are fun or oppressive.
- **Echo stacks hard with Thermal Shock**: a Frozen, marked target takes 34 + 51. If The Hollow + Frostwarden + Emberguard deletes people, lower `EchoScale` (1.25?) or cap echo damage.
- Ice (Brittle) is untested for feel. If it's frustrating rather than funny, raise `traction` (0.12) or shorten ice (4s).
- Bullets count as "flying through" a zone when they pass over it, so a Frostwarden firing across an Undertow freezes it. That's intended to make zone reactions common; check it doesn't feel random.
- **Blinkstorm now also shoves** (explosion shove, ~12 at the center) on top of its 5s stun. Watch that stunned players don't get flung off ledges too often.
- Magnetize rubble arcs pick one target per pulse; 4 chunks around one player can add up (up to ~40 dps if they stand in the middle). Probably fine because it's easy to walk out, but watch it.
- Zombies points: 10 per pellet makes shotguns farm points. If that matters once wall buys exist, pay hits per shot or per damage instead.
- Reaction sounds are placeholders from the gun pack; check levels against gunfire, and replace Brittle's static and Shatter's thud when ice sounds exist.
- **Destructible props**: check how fast cover disappears. A 4-player match with miniguns could strip Frostgrave's rubble in a minute. Tune per-rule toughness or `BaseHealth` if maps open up too fast, and check the auto-setup console count per map.
- **Forest fires**: Oldwoods has ~80 trees within reach of each other, and a 30%/s spread could burn the whole map in one match. Maps reset every match (tour), so this is a one-match spectacle, not lasting damage. Judge it on whether it makes a *good* match; lower `SpreadChance` if the forest is gone before mid-match.
- **Destructible interior walls** (BlackOsuary's dungeon especially): check the boundary detection keeps the outer walls up on every map, and that maps don't turn into open fields too early (walls are ×2.2 toughness and leave stubs).
- Explosive barrels (Drowned Sanctum has 7) might chain-kill. That's intended chaos, but watch the damage.
- Blinkstorm's 5s stun now also Charges everyone it hits. Combined with Conduct that's a lot of lockdown; another reason to shorten it.
