# Modes & Match Flow

## Brawl campaign (the session structure)

- Players pick a target: **first to 3, 5 or 7 match wins**.
- It's a **tour** (like Gang Beasts): every match is a different map, and each map loads fresh (destruction, craters and so on reset).
- Each **match** is one map with a single health pool (100 HP per wizard heart). Last wizard standing wins the match.
- **Between matches:**
  - A quick scoreboard with a crown or medals for the leader.
  - **Boon draft:** each player picks 1 of 3 random boons for the next match (for example: +1 dash charge, bullets pierce once, start with a Rune Shard, 10% lifesteal). **Losers pick first** (catch-up).
  - Map rotation or vote; loadouts are kept, with an optional quick re-loadout.
- `WinManager` currently ends straight to the MainMenu. It needs a match-result, then next-match loop, and a campaign-state object that carries wins and boons through scene loads (DataManager is the natural home).

## Match length control (target 5–10 min per match)

- **Sudden death** at about minute 7:
  - The arena shrinks (a closing elemental storm ring that deals damage outside it), **or** everyone is reduced to critical health.
  - Item drops speed up and wonder weapons become common.
- There are no stock respawns. Teammates revive fallen wizards where they went down; revival grants 2 seconds of protection.

## Arena design for chaos

- **Ledges, pits and water:** falling off should deplete health. This makes knockback (now on every gun hit) and shove abilities hugely valuable (Smash / Gang Beasts ring-outs). Credit goes to the last attacker.
- **Map hazards / events:**
  - Frostgrave: slippery ice patches and icicle falls.
  - Stormspire: lightning strikes on telegraphed spots, which Conduct through water.
  - Cinder Crucible: rising lava.
  - Drowned Sanctum: rising tide that soaks everyone.
  - Fungal Hollow: spore bursts (poison).
- Destructible cover and explosive barrels (element barrels that feed reactions).

## Feel and flavor features

- **Announcer:** first blood, double/triple kill, REVENGE (kill your last killer), reaction callouts, "LAST STAND" when someone's on their final life.
- **Emotes / taunts** on the d-pad (ragdoll-ish goofy animations fit the Gang Beasts vibe).
- **Kill feed** with element and reaction icons.
- **Killcam-lite:** the final elimination already does slow-mo plus the fireball; add a quick zoom on the last kill of a match.

## Team Deathmatch

- 2v2 (or 2v1 with a handicap). Team colors are a ring or outline on each wizard; bullets keep the wizard color.
- One health pool per wizard, with teammate revives. A team is out when no teammate is standing.
- Friendly fire: bullets **off** by default, reactions **on** (toggle). Chaos stays, frustration drops. *(Hook built: `Teams.ReactionFriendlyFire` is enforced; `Teams.TeamOf` needs TDM to assign teams.)*
- Team-comp hints on wizard select *(built: role, comps, and a callout when an earlier pick completes a comp)* (see [Elemental Ecosystem § Team roles](elemental-ecosystem.md#4-team-roles-tdm-and-zombies)).

## Health and teammate revival (implemented)

- Wizard heart count is a health stat: **100 HP per heart**, in one continuous health pool; the HUD shows one life icon.
- At zero HP, a wizard goes down in place and cannot move, shoot, cast, throw items or collect pickups. Enemies ignore downed wizards.
- A standing teammate stays inside the **2.5-unit rescue circle for 4 continuous seconds**. Its green arc and percentage show progress; leaving the circle resets it. Extra helpers do not speed it up. Players on a different elevation or airborne cannot revive.
- Revive restores **50% of maximum HP**, in place, with **2 seconds of damage immunity**. There is no automatic respawn or bleed-out timer.
- If nobody on the team remains standing, all downed teammates are eliminated. Solo runs end on the first down; free-for-all wizards have no teammate to revive them.
- Heart Relic now adds **100 maximum and current HP**. Last Rites triggers at **30% health or below**, replacing its last-stock condition.
- Radius, duration, restored fraction, immunity and HP per heart are tunable on `PlayerHealthControl`.
- Validation: `ReviveGameplayChecks.Run` passed 37 Unity play-mode checks covering health scaling, input locks, rescue timing/interruption, pause, repeat downs, invulnerability, solo/free-for-all outcomes and team wipes. Rescue-circle waiting/progress visuals were also rendered and inspected in an isolated test scene.

## Zombies (co-op waves)

- **Humans versus zombies:** zero friendly fire, self-damage or environmental damage to humans. Only zombie-attributed attacks can hurt players; teammate effects cannot apply harmful statuses or knockback. See [Zombies ability compatibility](zombies-abilities.md) for implementation and verification.

- **Implemented pacing:** enemies stream in under an alive cap, with party-scaled counts, gradually rising damage/health/speed, and bosses every fifth wave. Start after 3 seconds; take an 8-second wave break (12 after bosses). Clearing the ten-wave run returns to the hall. See [balance pass 13](balance-log.md#pass-13-2026-10-01-brisk-co-op-wave-pacing) for exact values.
- **Planned variety:** spitter, exploder and elemental variants that are immune to their own element; the current large monster fills elite/boss slots.
- Economy, perks, Mystery Box, Enchanting Altar, downed/revive: see [Items & Drops](items-and-drops.md#zombies-economy-co-op).
- Map progression: doors and walls bought with points (DestructibleWall / WallProgressUI already exist). The Cinder Crucible Zombies prototype has 24 authored spawn entrances tied to its four breakable progression barriers: 7 starting-area points, then 3 central-passage points, 3 in each wing, and 8 in the far hall. Each entrance opens only after its required barriers break, chooses least-recently-used locations, keeps at least 8 units from every player, and requires a complete route to a standing teammate within 45 units. The far hall stays quiet until a teammate has entered its reachable side, which prevents stranded spawns across the existing ramp gap. Maps without authored entrances retain the configured random-area fallback.
- The existing scene `CinderCrucibleZombies` is the prototype, and the only map the War Table offers in Waves (Custodia Perpetua); PvP modes get the original `CinderCrucible` arena instead. Goblins (`GoblinHealth`, `GoblinChaseAI`, `GoblinSpawner`) are the current enemy base.
