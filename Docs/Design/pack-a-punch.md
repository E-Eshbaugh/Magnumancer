# Pack-a-Punch — the final-stage Runeforge

Status (2026-10-01): **step 1 implemented — points and presentation. First playable forge built** — see [Built: the first playable forge](#built-the-first-playable-forge-2026-10-01). It ships the baseline upgrade plus a curated set of nine **active-rune** power-ups; the per-weapon payloads and passive-rune rows below are still plan only.

## Built: the first playable forge (2026-10-01)

The active rune is what decides the power-up: a forged gun's hits become *payloads* that combo with the wizard's chosen active ability. Rather than build all 24 active rows at once, this pass picks the ones that read clearly in a horde fight and reward playing around the ability. Every other build still gets the forge baseline (×1.5 damage, ×1.5 magazine) and its forged weapon name.

| Wizard | Rune | Epithet | What the forged gun does |
|---|---|---|---|
| Emberguard | II Inferno Rounds | Furnace | During Inferno Rounds, every third payload adds 1s to it, up to +2s per cast. |
| Frostwarden | I Covenant of Frostgrave | Rampart | Forged rounds fly through your **own** ice walls (hold a chokepoint from behind one). A round that passed through adds a freeze counter and throws a splinter (50%) into one neighbour. |
| Frostwarden | III Flash Freeze | Deepwinter | The first payload on an encased enemy bursts its ice: max(30, shot) to up to 3 enemies within 2.5m. Once per encasing. |
| Voltborn | II Chain Surge | Conductor | Payloads mark a relay (crackling ring, 4s). The next Chain Surge with a living relay jumps one extra time. |
| Tidebound | II Undertow | Whirlpool | A payload on an enemy inside your whirlpool splashes 50% to up to 3 others trapped in it. |
| Granite Vow | II Earthwork Parapet | Highground | Firing from on top of your parapet: payloads kick rubble for 50% into up to 3 enemies around the target. |
| Verdant Circle | I Seed of Aloria | Sanctuary | A forged kill inside a Seed totem's circle heals every standing teammate in it 5 HP (max once per payload interval). |
| Blightward | III Contagion | Outbreak | A payload on one of your infected enemies leaps the infection (remaining time) to a fresh enemy up to 6m away. Each infection leaps once; 3 leaps per Contagion cast. |
| The Hollow | II Soul Swap | Usurper | For 4s after a successful swap, every forged hit on the swapped enemy deals +50% (not payload-gated: swap a boss, then unload). |

Why these: each one is visible (zaps, bursts, rings), changes how you use the ability rather than adding a hidden number, and works in co-op Zombies. Skipped for now: rows that only nudge timers (Trailblazer, Aftershock, Backwash, Slipstream), Bulwark (goblins melee, so Bastion rarely blocks anything) and rows that need new trackers (Fencewalker, Riftwalker, Fractured, Starfall).

**How it plays.** Every Zombies map gets a Runeforge. Unless one is placed in the scene, it rises out of the floor 3.5–7m from the party once **wave 5 is cleared**, with a short "THE RUNEFORGE AWAKENS" announcement. That wave threshold stands in for the final-stage signal this plan asks for (none exists yet); `Runeforge.UnlockAll()` is the hook for a real stage controller, and `unlockAfterWave = 0` opens it from the start for testing. Within 2.5m a standing wizard sees the forged name, effect and price over their head; **hold X for 0.75s** to buy (a gold ring fills at their feet). X doesn't reload while you can buy; leaving, going down or releasing cancels for free, and swapping guns mid-hold needs a fresh press. Already-forged guns say so; a wonder weapon in hand says "Equip a loadout weapon".

**Rules as built.** Price 2,500 per gun, one forge per loadout slot, kept through swaps, downs and revives for the run. Forging changes only the player's runtime copy of the gun and refills that slot once. Payloads: forged primary rounds (both Akimbo hands) hitting an enemy, at most one per owner per 0.75s. Secondary damage goes through `DamageEvents.Deal`, so it can never cause another payload; it pays kill points but not per-hit points (`ForgedRunes.DealingSecondary`). Code: `Items/Runeforge/ForgedRunes.cs` (catalogue, dispatcher, the nine effects) and `Runeforge.cs` (box, unlock, buying).

**Deviations from the plan below.** Secondary damage is **50%** of the triggering shot, not 25%: with no per-weapon payloads yet, the rune effect is the whole power-up and 25% of a rifle round (~4 damage) was invisible. Sanctuary heals 5 HP rather than 3. Unlock is a wave threshold, not a stage. Launchers (Emberblast) get the baseline only: grenades aren't bullets, so they don't carry payloads yet. Healing/lifesteal passives (Leech Spores) still see secondary damage.

## Step 1: the wallet

Zombies begins at **0 points per player**. The existing rewards remain: **10 per damaging hit, 60 per kill, 30 per reaction, 25 × combo length when a reaction combo ends**. Double Points multiplies earnings, never prices. Each wizard has a persistent gold total directly under their corner crest. Every successful transaction shows the current balance and signed activity above that wizard; repeated transactions reuse the same display. Earnings and spending have separate subtotals, so a purchase cannot be hidden by simultaneous kills. It follows the wizard, waits **1.25 seconds** after the last transaction, then fades over **0.45 seconds**. Pausing pauses the fade. No economy HUD in brawl modes.

`ZombiesPoints.TrySpend(player, cost)` is the purchase entry point. It rejects nonpositive costs, missing/non-player owners, downed/inactive players and insufficient balances. Guns/child objects resolve to the owning health component. Rejected purchases do not emit feedback. Single scene loads reset wallets and the multiplier; additive scenery does not. Downing/reviving does not erase points. Damage-over-time ticks retain the existing hit reward for this first pass; reward normalization is a separate economy decision.

## Final-stage box and buying flow (next step)

- **Unlock on entering the final map stage**, not on clearing the last wave. The current ten-wave run ends when its last wave dies; unlocking then would leave no time to use the weapon. Treat “stage” as the final accessible arena/room for this plan. The map needs an explicit final-stage progression signal; a wave number or camera position is not a substitute.
- Place one **Runeforge** in that stage, visible but closed until the gate opens. Subscribe its unlock to progression and also read the current progression state on enable, so loading/re-enabling cannot miss the unlock. The existing `DestructibleWall.onWallDestroyed` can feed a stage controller. Do not infer the final wall from scene search order.
- Shared unlock, individual purchases. Proposed first-pass cost: **2,500 points per carried gun**, one upgrade per slot. This is provisional: at 80 points for a two-hit kill it is roughly 32 kills before reactions/combos. Four-player ten-wave earning telemetry must confirm everyone can buy at least one upgrade after reaching the forge. Tune price or final-stage arrival timing, rather than unlocking after victory.
- Within 2.5 units, a standing wizard sees the held gun's exact upgraded name, rune pair, effect summary and price. **Hold X for 0.75 seconds** to buy; the contextual interaction suppresses reload while held in forge range. Moving away, swapping weapons, taking a down or releasing cancels without charge. The existing controls use X for reload, so this arbitration is required.
- Validate stage, range, standing state, original slot identity, eligibility and price when the hold completes. Prepare the runtime weapon and effects before debiting. Debit once and install immediately in the same operation. No gun removal, waiting queue or lost-weapon pickup; two players may buy independently. Failed preparation leaves the original gun and balance intact.
- The preview says “Already forged” for an upgraded slot. Temporary wonder weapons say “Equip a loadout weapon”; the five wonder pickups have finite-ammo roles and are not forge inputs. Also exclude `Rework_FindModels` placeholder/melee assets, which are not selectable loadout guns.
- Upgrade survives swapping, downing and revival for that run; resets with a new run. Refill the upgraded slot once on purchase. Keep weapon family, weight, firing mode and LT ability, including backward off-hand Akimbo and Emberblast's Remote Fuse.
- Forge art: closed stone-and-brass chest; at unlock the lid splits around a floating gun, eight elemental grooves light, and the buyer's rune shade runs through the preview. One short unlock announcement; no permanent text above players.

## Complete variant recipe

The shipped loadout catalogue has **12 weapons × 8 wizards × 3 active runes × 2 passive runes = 576 combinations**. The tables below specify all of them compositionally, with no generic fallback: select the exact weapon row in the wizard's table, apply that wizard's active-rune row, then its passive-rune row. The rune effects **add to** the weapon's distinct delivery behavior. They do not replace the wizard's existing ability/passive or grant an unselected rune.

Display name: **weapon form · active epithet / passive epithet**. Example: Emberguard's Bloodcurrent with Inferno Rounds + Kindling becomes **Cinderstream · Furnace / Bellows**; with Blazing Ruin + Brand it becomes **Cinderstream · Trailblazer / Branded**. Both preserve Bloodcurrent's torrent/Akimbo identity, but reward different play patterns.

All numerical effects below are **initial tuning proposals**. Baseline forged stats: ×1.5 base damage, ×1.5 magazine capacity (round up), unchanged rate/reload/weight. Special secondary damage is a fraction of that forged shot's damage; do not apply the baseline twice. Preserve existing affinities and rune multipliers with explicit named modifier keys. Never edit shared `WeaponData` assets at runtime.

### Shared trigger definitions and limits

- **Hit:** a real enemy hit by the owner's primary weapon shot. Assign a shot ID at trigger pull; shotgun pellets, pierces and both Akimbo streams cannot multiply a once-per-shot proc. Launcher detonations keep their original owner and shot ID.
- **Payload:** the special effect in a weapon row. Unless a row states a cadence, payload eligibility is at most once per owner per 0.75s; only damage, not a new payload, occurs on intervening hits. Rows saying “every N hits” count successful primary trigger pulls, not pellets. Sniper/shotgun payloads occur on the first valid victim in a volley. Launcher area application affects at most 6 enemies once each.
- Element statuses apply through existing `StatusEffects` / `ElementReactions` rules. Do not double-apply Brand or freeze counters already supplied by the equipped passive. Nature roots and Void marks remain deliberate, capped payload effects, never on every minigun bullet.
- Secondary damage cannot recursively trigger forge payloads, forge cooldown refunds, healing, ammo refunds or additional secondary shots. Preserve kill ownership and ordinary kill points, but do not fabricate damage/kill events to award extra currency. Define source metadata before implementing proc effects; damage amount alone cannot distinguish a primary hit from a secondary hit.
- Default secondary damage: **25% of the triggering shot**, within 2.5m, at most 3 secondary targets. Secondary chains have one hop. Default status duration: 2s; roots/stuns cap at 0.5s and have 3s per-target immunity after ending. Bosses take normal damage/status setup but are immune to forced reposition and get 75% shorter roots/stuns.
- Maximum 3 persistent weapon-created zones per owner, 3s lifetime, 0.5s tick interval; replace the oldest. Healing tops out at 5 HP/s from forge effects; shields at +20; active time extension at +2s per cast; cooldown refunds at 2s per cast. Ammo refunds never exceed magazine capacity and cannot trigger reload-complete passives.
- Existing ability damage and durations stay as built unless a rune row explicitly changes them. No friendly damage, teammate shove, invulnerability extension or infinite crowd-control/ammo loop.

## Emberguard — fire, brands and forward pressure

| Base weapon | Forged form | Weapon payload |
|---|---|---|
| Ironspike | Cinder Nail | Piercing ember nail brands up to two enemies in its line. |
| Ironchant | Furnace Hymn | Every fourth hit exhales a short forward fan of embers from the victim. |
| Scrapshot | Coalscatter | First pellet plants a glowing coal; the next primary hit bursts it. |
| Blackmaw | Kilnmaw | Three hits on one target vent a small fire burst behind it. |
| Scrapwind | Bellowsong | Every fourth hit leaves a brief burning wake between owner and victim. |
| Runepiercer | Pyre Verdict | A focused hit detonates up to three existing brands for 8 damage each. |
| Dustbreaker | Hearthbreaker | A shell stamps a burning semicircle just beyond the first victim. |
| Gravewhisper | Ashen Litany | Every fourth hit sprays three embers toward nearby branded enemies. |
| Hellthrasher | Hellforge | Every third shell deposits one small lava patch under the first victim. |
| Bloodcurrent | Cinderstream | Every sixth hit sends a flame arc to one nearby branded enemy. |
| Iron Tempest | Solar Crucible | Every tenth hit vents a fire cone; spin-down does not reset its counter. |
| Emberblast | Sunspitter | Remote detonation leaves a molten pool at each grenade, subject to zone cap. |

| Active rune | Epithet | Added behavior on this weapon |
|---|---|---|
| I — Blazing Ruin | Trailblazer | First payload hit within 3s after the fire dash extends the nearest owned lava trail segment by 1s; once per dash. |
| II — Inferno Rounds | Furnace | During Inferno Rounds, three payload hits extend its damage buff by 1s, up to +2s per cast; never refill again. |
| III — Fireball | Starfall | First three payload hits after casting seed one extra ember each on enemies; the next Fireball consumes each seed for +8 burst damage. Seeds expire after 8s and are not brands. |

| Passive rune | Epithet | Added behavior |
|---|---|---|
| I — Brand of Flereous | Branded | A weapon-triggered brand detonation spreads one brand to one unbranded enemy within 2.5m; 1s cooldown, cannot spread again. |
| II — Kindling | Bellows | Every third payload against a burning enemy refunds another 0.25s of active cooldown, within the shared 2s cap. |

## Frostwarden — preparation, cover and shatter

| Base weapon | Forged form | Weapon payload |
|---|---|---|
| Ironspike | Hoarfang | A nail pierces two enemies, adding one freeze counter to the second. |
| Ironchant | Winter Canticle | Every fourth hit sprays chilling fragments behind its victim. |
| Scrapshot | Hailbasket | A shell leaves a narrow slowing ice strip beyond impact. |
| Blackmaw | Rimejaw | Three hits on one target release a pulse chilling its nearest neighbor. |
| Scrapwind | Sleetwheel | Every fourth hit sends a low-damage shard to a chilled enemy. |
| Runepiercer | Absolute Zero | Scope Focus converts the shot into a heavy ice lance that can Shatter. |
| Dustbreaker | Glacier Knell | Shell impact launches one heavy shard through the target to Shatter a frozen enemy behind it. |
| Gravewhisper | Silent Winter | Every fourth hit stores a shard; next reload releases up to three toward enemies ahead. |
| Hellthrasher | Whiteout Engine | Every third shell fans out ice spikes applying one counter per victim. |
| Bloodcurrent | Sleetvein | Every sixth hit transfers one counter to an unchilled nearby enemy without removing the original. |
| Iron Tempest | Avalanche Choir | Every tenth hit fires a heavy icicle alongside the stream. |
| Emberblast | Permafrost Mortar | Grenades become frost bursts leaving slippery, slowing frost patches. |

| Active rune | Epithet | Added behavior on this weapon |
|---|---|---|
| I — Covenant of Frostgrave | Rampart | Shots crossing your own ice wall pass through and gain a 25% damage ice splinter; once per payload interval, capped one extra target. Enemy shots stay blocked. |
| II — Winter's Wind | Northwind | First payload to hit a gust-displaced enemy within 3s sends a heavy shard back down the gust lane, making a Shatter follow-up. |
| III — Flash Freeze | Deepwinter | First payload hit on a Flash-Frozen victim releases its shatter damage in a 2.5m burst without adding counters or extending freeze. Once per frozen victim per cast. |

| Passive rune | Epithet | Added behavior |
|---|---|---|
| I — Fractalshot Shield | Faceted | Shattering a target adds 5 shield, capped +20 from the forge and once per second. |
| II — Cold Precision | Measured | Payloads pierce one additional chilled enemy at 25% damage; preserve normal counter bonuses, with no extra counter per pellet. |

## Voltborn — movement and sustained charge

| Base weapon | Forged form | Weapon payload |
|---|---|---|
| Ironspike | Stormneedle | Piercing nail bridges lightning between its first two victims. |
| Ironchant | Thunderverse | Every fourth hit emits one short chain arc. |
| Scrapshot | Sparkbroom | Shell impact fans three short sparks into nearby charged enemies. |
| Blackmaw | Voltmaw | Three hits on one target discharge its charge into a neighbor. |
| Scrapwind | Dynamo Rattle | Every fourth hit adds a seeking spark toward the nearest charged target. |
| Runepiercer | Horizon Bolt | Scoped shot becomes a lightning rail through up to three targets. |
| Dustbreaker | Thunderclap | A shell makes a close-range shock ring at the first impact. |
| Gravewhisper | Storm Gospel | Every fourth hit fires a fork toward two distinct charged targets. |
| Hellthrasher | Arc Furnace | Every third shell creates a short-lived electric strip across its impact fan. |
| Bloodcurrent | Livewire | Every sixth hit arcs between the latest front and rear Akimbo victims when both exist. |
| Iron Tempest | Thunderhead | Every tenth hit discharges a bolt into a charged enemy ahead. |
| Emberblast | Stormcell Lobber | Remote grenades briefly link nearby blast centers with lightning, max three links. |

| Active rune | Epithet | Added behavior on this weapon |
|---|---|---|
| I — Blinkstorm | Aftershock | First payload within 3s after Blinkstorm calls a 25% echo pulse at its departure point; no extra stun. |
| II — Chain Surge | Conductor | Payloads tag one relay target for 4s. Chain Surge may arc one extra time through that relay, then consumes it. |
| III — Stormrunner | Fencewalker | A payload hitting within 2m of your lightning fence sends a 25% pulse along that fence; one pulse per second, no additional stun. |

| Passive rune | Epithet | Added behavior |
|---|---|---|
| I — Lightning Reflex | Quickflash | The existing post-dash bolt may branch once for 25% damage to a charged enemy. No second bolt or additional dash proc. |
| II — Overcharge | Dynamo | At full Overcharge, every third payload preserves charge for 1s after firing stops, letting a short reposition retain the buildup. |

## Tidebound — positioning, decoys and reload rhythm

| Base weapon | Forged form | Weapon payload |
|---|---|---|
| Ironspike | Brine Harpoon | Harpoon pierces two targets, soaking the second. |
| Ironchant | Tide Hymn | Every fourth hit pushes a small soaking wave beyond the victim. |
| Scrapshot | Saltbreaker | A shell spreads a shallow soaking crescent at impact. |
| Blackmaw | Undertow Jaw | Three hits on one target tug nearby zombies toward it by 1m. |
| Scrapwind | Foamwheel | Every fourth hit splashes a second enemy, prioritizing unsoaked targets. |
| Runepiercer | Abyssal Lance | Scope Focus drives a narrow water jet through three enemies. |
| Dustbreaker | Breakwater | A shell launches a short wave that gathers enemies toward its centerline. |
| Gravewhisper | Drowned Psalm | Every fourth hit bounces a water bead between two soaked enemies. |
| Hellthrasher | Riptide Engine | Every third shell sends a rolling wave that pushes zombies 1m along its shot direction. |
| Bloodcurrent | Tidal Artery | Every sixth hit splashes around the owner, rewarding close moving fights. |
| Iron Tempest | Monsoon | Every tenth hit paints a brief soaking puddle under the victim. |
| Emberblast | Maelstrom Mortar | Each blast pulls zombies 1m toward its center before its soaking burst. |

| Active rune | Epithet | Added behavior on this weapon |
|---|---|---|
| I — Ves'shara's Reflection | Mirrored | Payload hits on enemies distracted by your living clone splash one additional enemy. The clone remains a decoy, never fires damaging duplicate guns. |
| II — Undertow | Whirlpool | Payloads hitting an enemy inside your whirlpool deal their secondary damage to one additional trapped target. |
| III — Riptide | Backwash | First payload after the outward surge leaves a water wake; returning through it refunds one shell or 10% magazine (round up), once per cast. |

| Passive rune | Epithet | Added behavior |
|---|---|---|
| I — Undercurrent | Slipstream | First payload within the post-dash speed window extends that window by 0.5s, once per dash. |
| II — Tidal Momentum | Flowing | First payload after a dash shoots a 25% water echo; consume one post-dash charge, not additional ammunition. |

## Granite Vow — deliberate fire and holding ground

| Base weapon | Forged form | Weapon payload |
|---|---|---|
| Ironspike | Bedrock Nail | Heavy stone nail staggers its first victim and pierces one more. |
| Ironchant | Quarry Hymn | Every fourth hit scatters sharp rubble behind its victim. |
| Scrapshot | Gravel Choir | A shell throws a ground-level cone of stagger fragments. |
| Blackmaw | Faultjaw | Three hits on one enemy crack the ground in a short line behind it. |
| Scrapwind | Mason's Rattle | Every fourth hit fires a ricocheting stone into a staggered enemy. |
| Runepiercer | Mountain Verdict | Focused shot becomes a heavy obsidian lance with one extra pierce. |
| Dustbreaker | Tectonic Bell | Shell impact sends a narrow heavy ground shockwave. |
| Gravewhisper | Lithic Testament | Every fourth hit lodges a shard; next hit on that target breaks it into rubble. |
| Hellthrasher | Quakehammer | Every third shell sends three stagger spikes across its impact fan. |
| Bloodcurrent | Flintpulse | Every sixth hit chips a staggered enemy, scattering stones toward its nearest neighbor. |
| Iron Tempest | Walking Citadel | Every tenth hit releases a heavy stone slug through the bullet stream. |
| Emberblast | Worldbreaker | Grenade bursts launch three heavy rubble fragments, capped distinct targets. |

| Active rune | Epithet | Added behavior on this weapon |
|---|---|---|
| I — Seismic Judgement | Faultline | First payload on each quake-staggered enemy within 3s emits a small ground aftershock; max three aftershocks per cast. |
| II — Earthwork Parapet | Highground | While standing on your parapet, payloads striking ground enemies kick rubble upward through one extra target. No global airborne bonus. |
| III — Bastion Stance | Bulwark | Each blocked hostile attack stores one shard, max three; the next payload spends them as a fan at 10% damage each. Clear on stance end. |

| Passive rune | Epithet | Added behavior |
|---|---|---|
| I — Stonebind | Anchored | At full standing armor, payload radius grows by 0.5m. Moving removes that bonus normally; no permanent armor. |
| II — Fortified | Mason | First payload while the reload shield is active adds 5 shield, once per completed reload and within the +20 forge cap. |

## Verdant Circle — sustain and traps with an offensive payoff

| Base weapon | Forged form | Weapon payload |
|---|---|---|
| Ironspike | Thornspike | Piercing thorn leaves a seed on the first victim; its death heals the owner 3 HP within 8m. |
| Ironchant | Grove Hymn | Every fourth hit sends a thorn to an enemy near an owned nature zone. |
| Scrapshot | Briar Basket | A shell plants a tiny bramble that roots one pursuing enemy once. |
| Blackmaw | Rootmaw | Three hits on a victim briefly root it and burst thorns behind it. |
| Scrapwind | Petalwheel | Every fourth hit fires a petal into another enemy; return visual delivers 2 HP to owner. |
| Runepiercer | Heartwood Lance | Focused shot pierces three enemies and plants one healing seed at its final victim. |
| Dustbreaker | Bramble Knell | Shell impact spreads a short thorn ridge, damaging crossing enemies. |
| Gravewhisper | Green Testament | Every fourth hit plants a seed; killing that victim sends a thorn to a second seeded enemy. |
| Hellthrasher | Thornthresher | Every third shell sweeps a wide bramble fan with one brief root on its first victim. |
| Bloodcurrent | Sapstream | Every sixth hit drains 3 HP from a nearby rooted enemy to its owner. |
| Iron Tempest | Canopy Engine | Every tenth hit releases a heavy wooden splinter toward a trapped enemy. |
| Emberblast | Seedfall Mortar | Detonations plant short bramble circles, each with one root charge. |

| Active rune | Epithet | Added behavior on this weapon |
|---|---|---|
| I — Seed of Aloria | Sanctuary | Payload kills within the totem's radius emit a 3 HP pulse to standing allies inside; once per second, shared forge healing cap per recipient. |
| II — Thornsnare | Snarekeeper | First payload on your snared target sends thorns to two nearby enemies; it does not re-root or rearm the trap. |
| III — Siphoning Vine | Lifeline | Payloads on your tethered target restore 2 HP to the lowest-health standing ally within 5m of you; no tether extension or range increase. |

| Passive rune | Epithet | Added behavior |
|---|---|---|
| I — Verdant Resurgence | Regrowth | While natural regeneration is active, payload kills grow one 3 HP seed for the owner; max one per second. Does not accelerate the undamaged timer. |
| II — Thornhide | Barbed | Close-range retaliation primes one thorn for the next payload, dealing 25% extra secondary damage. One stored charge, 3s expiry; reflected damage cannot reprime itself. |

## Blightward — spread, proximity and controlled sustain

| Base weapon | Forged form | Weapon payload |
|---|---|---|
| Ironspike | Plague Needle | Poison needle pierces one extra enemy, carrying poison to it. |
| Ironchant | Pestilent Hymn | Every fourth hit sprays poison behind the victim. |
| Scrapshot | Sporebasket | A shell leaves a small short-lived poison patch at first impact. |
| Blackmaw | Rotmaw | Three hits on a poisoned enemy burst a pustule onto one neighbor. |
| Scrapwind | Miasma Rattle | Every fourth hit sends a spore toward an unpoisoned nearby enemy. |
| Runepiercer | Patient Zero | Focused hit plants a cyst; death bursts poison within 2.5m once. |
| Dustbreaker | Blightbell | A shell blows a short poison cone through its first victim. |
| Gravewhisper | Fever Psalm | Every fourth hit marks a carrier whose next primary hit spreads poison once. |
| Hellthrasher | Fumigator | Every third shell exhales a lingering toxic fan at impact. |
| Bloodcurrent | Venomvein | Every sixth hit compresses existing poison into a 25% damage sting, without consuming poison. |
| Iron Tempest | Carrion Engine | Every tenth hit launches one spore pod that bursts on enemy contact. |
| Emberblast | Viper Crucible | Remote blasts overlap into one stronger poison patch, capped normal total tick damage rather than stacked puddles. |

| Active rune | Epithet | Added behavior on this weapon |
|---|---|---|
| I — Viper's Nest | Nestkeeper | Payload hits on enemies within 3m of an owned armed mine prime its next blast for +25% damage; one charge per mine, no forced detonation. |
| II — Plaguebearer | Plaguewalk | Payloads on enemies inside the plague aura add one extra plague stack, once per enemy per second; use the existing stack cap. |
| III — Contagion | Outbreak | First payload on each infected victim emits a one-time spore toward one uninfected neighbor; max three spreads per cast, using remaining infection lifetime. |

| Passive rune | Epithet | Added behavior |
|---|---|---|
| I — Virulent Shroud | Shrouded | A qualifying nearby kill enlarges its existing death-spore radius by 0.5m. Do not spawn a second cloud. |
| II — Leech Spores | Sanguine | First payload each second on a poisoned victim heals an extra 2 HP; additional forge damage is excluded from recursive lifesteal. |

## The Hollow — marks, risk and relocation

| Base weapon | Forged form | Weapon payload |
|---|---|---|
| Ironspike | Nullspike | A nail pierces one extra enemy and places one short void mark on its final victim. |
| Ironchant | Hollow Hymn | Every fourth hit sends a void splinter toward a marked enemy. |
| Scrapshot | Graveglass | First pellet lodges a shard; victim death shatters it toward one marked neighbor. |
| Blackmaw | Event Maw | Three hits on a marked victim cause a small inward void pulse. |
| Scrapwind | Wraith Rattle | Every fourth hit bends a 25% void echo toward a marked target. |
| Runepiercer | Final Argument | Focused shot pierces three targets and empowers one existing mark's eventual burst by 25%. |
| Dustbreaker | Death Knell | Shell impact sends a heavy void crescent through the first marked target. |
| Gravewhisper | Last Testament | Every fourth hit stores a soul splinter; reload fires up to three at marked enemies. |
| Hellthrasher | Oblivion Press | Every third shell makes a brief inward pull at impact, then a void burst. |
| Bloodcurrent | Revenant Vein | Every sixth hit sends a soul echo between the latest front/rear victims, if one is marked. |
| Iron Tempest | Nightfall Engine | Every tenth hit fires a void lance down the current firing lane. |
| Emberblast | Eclipse Mortar | Remote blasts tug enemies inward, then apply one short void mark per victim. |

| Active rune | Epithet | Added behavior on this weapon |
|---|---|---|
| I — Soulfracture Beam | Fractured | First payload on a beam-marked enemy stores a 25% splinter released at its death toward a second marked enemy. A splinter cannot store another. |
| II — Soul Swap | Usurper | For 4s after a successful swap, the first three payloads against the swapped enemy gain +25% primary damage. Fizzled swaps grant nothing; no repeated health exchange. |
| III — Void Rift | Riftwalker | First payload within 3s of using your own rift fires one 25% echo from its entrance toward the original hit point. Max one echo per traversal and 1s cooldown; echo cannot traverse again. |

| Passive rune | Epithet | Added behavior |
|---|---|---|
| I — Last Rites | Defiant | While Last Rites is actually active, payloads pierce one additional enemy. Do not extend immunity or double damage again. |
| II — Soul Harvest | Reaping | At five harvest stacks, every third payload emits a 25% soul splinter. Losing the existing stacks removes this bonus immediately. |

## Implementation sequence and acceptance gates

1. **Wallet and displays (this change):** runtime-bind each participating wizard from `MultiplayerManager`; test real damage/kill rewards, four-player isolation, all transaction signs, invalid spends, pause/fade, re-enable, cap overflow and scene reset. Verify crest placement at the game's supported resolutions in the actual arena.
2. **Progression and forge shell:** introduce explicit stage state, authored final-stage placement, controller prompt/hold arbitration and atomic purchases. Test locked stage, entering final stage before last wave, multiple buyers, insufficient funds, cancellation, downing mid-hold and repeated holds. Reuse `TrySpend`; do not add a second wallet.
3. **Runtime upgrade state and catalogue:** key by original weapon asset GUID + `WizardData.passive` + active index + passive index. Store one runtime clone and upgrade state per loadout slot. Add a catalogue check enumerating all 576 choices; require unique display identities and complete valid recipes. Resolve rune choices from `DataManager` once per build, never from visual shade (duplicate wizards can receive another shade).
4. **Combat source metadata first:** primary shot/volley ID, original owner, weapon slot and secondary-effect flag must propagate through bullets, grenade fuses, impacts and damage-over-time. Add guarded payload dispatch after actual damage. Audit reentrant damage, dead targets, per-target deduplication, existing freeze/brand applications and reward attribution.
5. **Implement by wizard:** Emberguard and Frostwarden first to exercise statuses, then Tidebound/Granite for movement/cover, then Voltborn/Blightward/Verdant/Hollow for chain, sustain and portal guards. Preserve all 12 weapon delivery identities; test Rune I/II/III with Passive I/II on each family.
6. **Playtest:** record per-player points on final-stage arrival, time to first purchase, upgrade damage contribution, extra objects per second, 4-player frame time, healing/shield uptime and boss control uptime. Compare all six rune pairs against baseline. Confirm no infinite refunds, no recursive reactions, no shots blocked by your intended allied wall crossing, and no upgrade lost on swap/revive. Price and the proposed ×1.5 baseline remain provisional until this pass.

Sources of truth: `Assets/Resources/Weapons/{Initiate,Ascendant,Archon}`, `Assets/Resources/Wizards`, `RuneBook`, the current active/passive implementations, `AmmoControl`, `DamageEvents`, `ZombiesPoints`. Weapon inventory: Initiate — Ironspike/Ironchant/Scrapshot; Ascendant — Blackmaw/Scrapwind/Runepiercer/Dustbreaker; Archon — Gravewhisper/Hellthrasher/Bloodcurrent/Iron Tempest/Emberblast.
