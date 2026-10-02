# Game Feel: Hits, Music, Announcer, Round Flow

All of this lives in `Assets/Scripts/Feel/` and hooks in through `DamageEvents` and scene loads. Nothing has to be placed in a scene.

## Hit feel (`HitFeedback`)

- **Flash:** the victim's solid meshes swap to flat white for 70 ms. Monsters flash too.
- **Punch sound:** scaled to the damage. Heavy hits (20%+ of max health, or lethal) use the wet-impact set. Sounds are rate limited per victim, so poison ticks and shotgun pellets don't machine-gun.
- **Sparks:** a burst in the attacker's color at the victim's chest.
- **Camera shake:** scales with the hit. `CameraShake` now ignores a weak shake while a stronger one is still running.
- **Hitstop:** hits of 6%+ of max health freeze the game for 35–100 ms (capped at 130 ms, with a 120 ms cooldown).
  - Lethal hits skip it so the elimination slow-mo plays.
  - Hazards with no attacker never freeze.
- **Kill chime:** two bell notes when a player takes out another player.

Tune all of this with the constants at the top of `HitFeedback.cs`, and the clips and volume in `Resources/FeelSounds`.

## Music (`MusicDirector`, `ProceduralScore`)

- **Built-in synth score:** no assets needed.
  - Hall theme: D minor, 72 BPM, pads, sub bass and music-box bells. Plays on the title and in the Great Hall.
  - Battle theme: A minor, 140 BPM, transposed per arena visit.
- **Battle layers build through the round:**
  - Start: drums, bass and pad.
  - First blood: adds snare and arpeggio.
  - Final showdown: adds the lead melody and open hats.
- **Round end:** a synthesized fanfare plays for the winner, and a low sting for a draw.
- **Ducking:** the music dips under the announcer.
- **Real tracks:** drop clips into `Resources/FeelSounds` (`hallMusic`, `battleMusic[]`, `victoryStinger`) and they replace the synth.

## Announcer (`FeelAudio`, `FeelHud`)

- **Voice lines** are in `Resources/Announcer/*.wav`, loaded by file name.
  - They're placeholders rendered with macOS `say` (voice "Daniel"), pitched down with a reverb tail.
  - To replace one, drop in a file with the same name.
  - Lines: `round_1`..`round_20`, `round_final`, `fight`, `first_blood`, `double_kill`, `triple_kill`, `revenge`, `final_showdown`, `last_stand`, `match_point`, `draw`, `flawless`, `unstoppable`, `champion`, `wins_<wizard>`.
  - Apple's voices aren't licensed for shipping in a commercial game, so replace them before any release.
- **On-screen text:** every callout is also big outlined text. Banners slam in at screen center; callouts stack along the top.
- **Revenge** means killing whoever took you out last round, since a PvP round is now one life.
- **Last Stand** only fires in modes that still give extra lives.

## Brawl campaign (`MatchDirector`, `MatchSeries`)

- **A series is first to `MatchSeries.WinsNeeded`** (3) round wins. One round is played per map.
- **The tour rotates through `MatchSeries.TourMaps`**, currently only `CinderCrucible`; add arenas as they're ready.
- **Round flow:**
  1. Arrival.
  2. "ROUND N / FIRST TO 3" (or MATCH POINT), then "FIGHT!".
  3. Last wizard standing gets a spotlight, rings and the fanfare.
  4. "<WIZARD> / WINS ROUND N" (or FLAWLESS / UNSTOPPABLE).
  5. Scoreboard: win pips plus kills.
  6. After 6 s, or when someone presses A / Start / Space, the next map loads.
- **Champion:** a gold title, confetti, then back to the Great Hall.
- **Series reset:** going back to the Hall or the title resets the series.
- **`WinManager` still ends Zombies.** In PvP, `MatchDirector` switches it off and ends rounds itself.

## Not built yet

- Choosing 3/5/7 at the War Table (`MatchSeries.WinsNeeded` is ready for it).
- The boon draft between rounds.
- A kill feed.
- A killcam zoom.
