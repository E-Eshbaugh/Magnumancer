# Audio

Every moment that rumbles, flashes or shakes should also make a sound. This is how the game's
sound effects are wired, where the clips come from, and how to tune them.

## The pieces

| What | Where | Notes |
|---|---|---|
| Sound table | `Assets/Resources/SfxBank.asset` (`SfxBank.cs`) | Every effect's clips, volume, pitch, random pitch, max length, retrigger gap, voice cap and priority. Tune here in the inspector. Plus per-weapon-family handling clips (draw, dry fire, rack, slug load) |
| Player | `Assets/Scripts/Audio/Sfx.cs` | `Sfx.Play(SfxId, [position], volume, pitch)`, `Sfx.Gun(...)`, loops (`StartLoop/SetLoop/StopLoop`, owned by an object, stop when it dies), `PlayLate` |
| IDs | `Assets/Scripts/Audio/SfxId.cs` | Explicit numbers (the asset stores them): add new ones with a new number, never renumber |
| Director | `Assets/Scripts/Audio/SfxDirector.cs` | Boots itself in every scene. Hits, hit markers, kill dings, lives lost, kill-streak announcer, match-start gong, victory/defeat sting, Zombies points, War Table navigation. Adds `PlayerSfx` to players |
| Per player | `Assets/Scripts/Audio/PlayerSfx.cs` | Footsteps, jump / double jump / landing, dash, gun draw / dry fire / rack / slugs / last round, ability cast (by element) and "ready" ping, heal, low-health heartbeat, stun birdies, elimination toll |
| Gun shots and reloads | each `WeaponData` (`fireSound`, `reloadSound`) | Unchanged: played by `AmmoControl` through the player's own AudioSource |
| Reactions and combo callouts | `Resources/ReactionSounds.asset` (`ReactionAudio`) | Unchanged. Its empty item-drop slots now fall back to the SfxBank sounds (`ItemAudio`) |

Everything plays 2D (one shared couch screen) and pans a little toward the side of the screen it
happened on (`stereoSpread` on the bank). A pool of 24 voices with per-sound gaps and caps keeps four
players and a horde from turning into mush; when it's full the least important, oldest sound is cut.
Pausing holds the fight's sounds (`AudioListener.pause`); pause menu sounds still play.

The director and `PlayerSfx` only *listen* (DamageEvents, `OnDash`, `OnFired`, `OnReloaded`,
`OnAbilityActivated`, `OnHealthChanged`, `OnDeath`, public state), so gameplay scripts don't need to
know about audio. One-off moments call `Sfx.Play` directly: explosions (`Explosions.AffectWorld`, which
skips grenades/mines/Cursed bursts that play their own clip, and blasts a reaction already voiced),
props, bullet impacts, spawn bolt, wonder weapons, throwables, buffs, gun alt-abilities, a few rune
signatures, pause, the wave counter, the Great Hall stations and the title screen.

## Where the clips come from

- `Assets/Sounds/Gamemaster Audio - Gun Sound Pack/` — gun handling, explosions, whooshes, punches,
  zaps, flames, gas, birds, announcer.
- `Assets/Sounds/Magnumancer/sfx_*.wav` — synthesized by `Tools/Audio/synth_sfx.py` (pure Python,
  deterministic): footsteps, landings, hit marker, kill ding, soul/elimination, respawn, heal, ability
  ready, frost/water/void/nature/earth/arcane casts, blink, UI blips, match gong, victory/defeat,
  heartbeat, coins, pickups, bullet impacts, ricochets, shields, debris, wave drums, Gale Horn,
  minigun spin loop, stun, crown fanfare. Edit a recipe and re-run (`python3 Tools/Audio/synth_sfx.py
  heal` regenerates only matching names); GUIDs stay stable.
- `Tools/Audio/build_sfx_bank.py` wrote the initial `SfxBank.asset` from a table of clip patterns.
  Re-running it **overwrites inspector tuning**: once you've tuned in Unity, edit the asset instead.

## Not wired yet

- `DetonatorClick` (Remote Detonator) and the Akimbo draw: those scripts had uncommitted work when
  this was built; add `Sfx.Play(SfxId.DetonatorClick, ...)` / `SfxId.GunAbility` at their rumble calls.
- Zombies "downed" / revive (only in the uncommitted Zombies work): good spots are `OnDowned` /
  `OnRevived` on `PlayerHealthControl` (e.g. `LifeLost` at a lower pitch, then `Respawn`).
- Goblin growls / attack swipes: there are no creature vocal clips in the project yet.
- Music and ambience: none yet (the gun pack has ambience loops — cicadas, river, lake — that could
  sit under a map).
