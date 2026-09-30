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

## Open issues / to playtest

- **Blinkstorm stun is 5s** (`LightningBlastDamage`: `Stun(0.2f, 5f)`). With stuns now also blocking jumps that's very punishing. Suggest **~1.5s**.
- Check that gunfire knockback isn't too floaty or too weak (`knockbackPerDamage` 0.18). Should shotguns get an extra multiplier?
- Pack weight rarely triggers under current orb budgets (most loadouts total 5–12). Consider allowance 6.
- Match length is untested with 120 HP and 3–4 hearts. Add sudden death (see [Modes & Match Flow](modes-and-flow.md)) as the hard cap.
- Ability damage values (Rune II and III actives) haven't had a dedicated pass.
- Blightward (lime) and Verdant Circle (green) bullet colors are the closest pair; tweak a `themeColor` if they're hard to tell apart.
