# Modes & Match Flow

## Brawl campaign (the session structure)

- Players pick a target: **first to 3, 5 or 7 match wins**.
- It's a **tour** (like Gang Beasts): every match is a different map, and each map loads fresh (destruction, craters and so on reset).
- Each **match** is one map with stock rules (wizard hearts = lives). Last wizard standing wins the match.
- **Between matches:**
  - A quick scoreboard with a crown or medals for the leader.
  - **Boon draft:** each player picks 1 of 3 random boons for the next match (for example: +1 dash charge, bullets pierce once, start with a Rune Shard, 10% lifesteal). **Losers pick first** (catch-up).
  - Map rotation or vote; loadouts are kept, with an optional quick re-loadout.
- `WinManager` currently ends straight to the MainMenu. It needs a match-result, then next-match loop, and a campaign-state object that carries wins and boons through scene loads (DataManager is the natural home).

## Match length control (target 5–10 min per match)

- **Sudden death** at about minute 7:
  - The arena shrinks (a closing elemental storm ring that deals damage outside it), **or** everyone drops to their last life.
  - Item drops speed up and wonder weapons become common.
- Respawn at the **spawn point farthest from enemies** (currently each player's own start spot) to avoid spawn-camping; 2s spawn invulnerability is already in.

## Arena design for chaos

- **Ledges, pits and water:** falling off costs a life. This makes knockback (now on every gun hit) and shove abilities hugely valuable (Smash / Gang Beasts ring-outs). Credit goes to the last attacker.
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
- Shared team lives or per-player lives (test both). A team is out when all of its lives are gone.
- Friendly fire: bullets **off** by default, reactions **on** (toggle). Chaos stays, frustration drops. *(Hook built: `Teams.ReactionFriendlyFire` is enforced; `Teams.TeamOf` needs TDM to assign teams.)*
- Team-comp hints on wizard select *(built: role, comps, and a callout when an earlier pick completes a comp)* (see [Elemental Ecosystem § Team roles](elemental-ecosystem.md#4-team-roles-tdm-and-zombies)).

## Zombies (co-op waves)

- CoD Zombies structure: waves with rising health and count, special zombies (bruiser, spitter, exploder, elemental variants that are *immune* to their own element), and boss waves every 5.
- Economy, perks, Mystery Box, Enchanting Altar, downed/revive: see [Items & Drops](items-and-drops.md#zombies-economy-co-op).
- Map progression: doors and walls bought with points (DestructibleWall / WallProgressUI already exist).
- The existing scene `CinderCrucibleZombies` is the prototype. Goblins (`GoblinHealth`, `GoblinChaseAI`, `GoblinSpawner`) are the current enemy base.
