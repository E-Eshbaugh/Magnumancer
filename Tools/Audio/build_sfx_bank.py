#!/usr/bin/env python3
"""
Builds Assets/Resources/SfxBank.asset (the clip/volume table Sfx plays from) by looking up
each clip's GUID in its .meta file. Run once to (re)create the bank:

    python3 Tools/Audio/build_sfx_bank.py

After that, tune volumes and swap clips in Unity's inspector. Re-running this OVERWRITES
inspector changes, so either keep the table below in sync or stop using the script.
Needs the synthesized clips first (Tools/Audio/synth_sfx.py) and the .cs.meta for SfxBank.
"""
import glob
import os
import re
import sys

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), "..", ".."))
SOUNDS = os.path.join(ROOT, "Assets", "Sounds")
GUN = os.path.join(SOUNDS, "Gamemaster Audio - Gun Sound Pack", "Gun")
BONUS = os.path.join(SOUNDS, "Gamemaster Audio - Gun Sound Pack", "Bonus_Sounds")
MINE = os.path.join(SOUNDS, "Magnumancer")
OUT = os.path.join(ROOT, "Assets", "Resources", "SfxBank.asset")

PREFIX = {"G": GUN, "B": BONUS, "M": MINE, "S": SOUNDS}


def enum_values():
    src = open(os.path.join(ROOT, "Assets", "Scripts", "Audio", "SfxId.cs")).read()
    sfx = src[src.index("enum SfxId"):src.index("enum GunSfx")]
    return {m.group(1): int(m.group(2)) for m in re.finditer(r"(\w+)\s*=\s*(\d+)", sfx)}


def guid(path):
    with open(path + ".meta") as f:
        for line in f:
            if line.startswith("guid:"):
                return line.split()[1]
    raise RuntimeError("no guid in " + path)


def clips(*patterns):
    """'B:whoosh_swish_small_0*' -> sorted matching files (exact names allowed too)."""
    out = []
    for p in patterns:
        where, pat = p.split(":", 1)
        # the "[AudioTrimmer.com]" copies are only used when asked for by name
        found = sorted(f for f in glob.glob(os.path.join(PREFIX[where], pat))
                       if f.endswith((".wav", ".mp3", ".ogg")) and ("AudioTrimmer" not in f or "AudioTrimmer" in pat))
        if not found:
            sys.exit(f"no clips match {p}")
        out += found
    return out


def S(clip_patterns, volume=0.6, pitch=1.0, jitter=0.05, max_length=0.0, gap=0.03, voices=4, priority=1):
    if isinstance(clip_patterns, str):
        clip_patterns = [clip_patterns]
    return dict(clips=clips(*clip_patterns), volume=volume, pitch=pitch, jitter=jitter,
                max_length=max_length, gap=gap, voices=voices, priority=priority)


# ------------------------------------------------------------------ the table

SOUNDS_TABLE = {
    # Movement
    "Footstep":     S("M:sfx_footstep_*", 0.22, jitter=0.08, gap=0.05, voices=4, priority=0),
    "Jump":         S("B:whoosh_swish_small_0*", 0.22, pitch=1.15, max_length=0.4, priority=0),
    "DoubleJump":   S("M:sfx_double_jump.wav", 0.35, priority=0),
    "Land":         S("M:sfx_land_*", 0.3, jitter=0.08, gap=0.05, priority=0),
    "LandHeavy":    S("M:sfx_land_*", 0.55, pitch=0.85, gap=0.05),
    "Dash":         S("B:whoosh_swish_high_fast_0*", 0.42, max_length=0.6, gap=0.05),
    "Blink":        S("M:sfx_blink.wav", 0.5, gap=0.05),

    # Guns
    "BulletImpact": S("M:sfx_bullet_impact_*", 0.16, jitter=0.12, gap=0.03, voices=4, priority=0),
    "Ricochet":     S("M:sfx_ricochet_*", 0.18, jitter=0.1, gap=0.15, voices=2, priority=0),
    "MagEmpty":     S("G:gun_revolver_pistol_dry_fire_0*", 0.45, pitch=1.25, gap=0.05),
    "GunAbility":   S("B:powerup_whiz_nightvision_goggles_on_01.wav", 0.35, gap=0.1),
    "ScopeFocus":   S("G:gun_rifle_sniper_scope_zoom_lens_0*", 0.5, gap=0.2),
    "Overclock":    S("B:gear_drill_turn_0*", 0.5, pitch=1.2, max_length=1.2, gap=0.3),
    "LauncherThunk": S("G:weapon_cannon_shot_0*", 0.35, pitch=1.5, max_length=0.7, gap=0.05),
    "DetonatorClick": S("B:switch_button_push_on_off_0*", 0.6, gap=0.05),

    # Combat
    "HitMarker":    S("M:sfx_hitmarker.wav", 0.35, jitter=0.03, gap=0.05, voices=3),
    "Hurt":         S("B:punch_general_body_impact_0*", 0.4, jitter=0.08, gap=0.06, voices=3),
    "KillConfirm":  S("M:sfx_kill_confirm.wav", 0.55, jitter=0.0, gap=0.05, priority=2),
    "LifeLost":     S("M:sfx_death_soul.wav", 0.7, jitter=0.03, gap=0.1, priority=2),
    "Eliminated":   S("M:sfx_eliminated.wav", 0.8, jitter=0.0, gap=0.3, priority=2),
    "Respawn":      S("M:sfx_respawn.wav", 0.45, jitter=0.02, gap=0.05),
    "Heal":         S("M:sfx_heal.wav", 0.4, jitter=0.02, gap=0.4),
    "Heartbeat":    S("M:sfx_heartbeat.wav", 0.7, jitter=0.0, gap=0.5, priority=2),
    "MonsterHit":   S("B:punch_grit_wet_impact_0*", 0.28, jitter=0.1, gap=0.05, voices=3, priority=0),
    "MonsterDeath": S("B:punch_grit_wet_impact_0*", 0.5, pitch=0.75, jitter=0.1, gap=0.05, voices=3),
    "Explosion":    S("B:explosion_small_0*", 0.55, jitter=0.08, gap=0.05, voices=3, priority=2),
    "ExplosionBig": S("B:explosion_large_0*", 0.7, jitter=0.06, gap=0.08, voices=3, priority=2),
    "Quake":        S(["B:explosion_deep_low_1.wav", "B:explosion_far_distant_0*"], 0.45, pitch=0.8, gap=0.35, voices=2),
    "PropHit":      S("M:sfx_prop_hit_*", 0.25, jitter=0.12, gap=0.06, voices=3, priority=0),
    "PropBreak":    S("M:sfx_debris_*", 0.6, jitter=0.08, gap=0.05, voices=3),
    "Stun":         S("B:bird_small_songbird_call_chirp_0*", 0.3, pitch=1.2, jitter=0.1, max_length=0.9, gap=0.15, voices=2),

    # Wizard abilities
    "CastFire":     S("B:gas_large_flame_ignite_0*", 0.6, max_length=1.2, gap=0.05),
    "CastFrost":    S("M:sfx_cast_frost.wav", 0.55, gap=0.05),
    "CastWater":    S("M:sfx_cast_water.wav", 0.6, gap=0.05),
    "CastLightning": S("B:taser_stun_gun_zap_electricity_0*", 0.5, max_length=0.8, gap=0.05),
    "CastEarth":    S("M:sfx_cast_earth.wav", 0.6, gap=0.05),
    "CastNature":   S("M:sfx_cast_nature.wav", 0.55, gap=0.05),
    "CastPoison":   S("B:gas_leak_short_burst_0*", 0.5, gap=0.05),
    "CastVoid":     S("M:sfx_cast_void.wav", 0.55, gap=0.05),
    "CastArcane":   S("M:sfx_cast_arcane.wav", 0.5, gap=0.05),
    "AbilityReady": S("M:sfx_ability_ready.wav", 0.3, jitter=0.0, gap=0.1),
    "Zap":          S("B:taser_stun_gun_zap_electricity_0*", 0.32, jitter=0.1, max_length=0.45, gap=0.08, voices=3),
    "Splash":       S("M:sfx_cast_water.wav", 0.4, pitch=1.2, jitter=0.1, gap=0.08, voices=3),
    "Gust":         S("B:whoosh_swish_high_fast_0*", 0.55, pitch=0.7, gap=0.08),
    "GasBurst":     S("B:gas_leak_short_burst_0*", 0.4, jitter=0.1, gap=0.1),

    # Items
    "DropIncoming": S("M:sfx_drop_incoming.wav", 0.5, jitter=0.0, gap=0.3),
    "DropLand":     S("B:explosion_far_distant_0*", 0.6, gap=0.2),
    "Pickup":       S("M:sfx_pickup.wav", 0.5, jitter=0.03, gap=0.05),
    "PickupRare":   S("M:sfx_pickup_rare.wav", 0.6, jitter=0.0, gap=0.2, priority=2),
    "CrownTaken":   S("M:sfx_crown.wav", 0.6, jitter=0.0, gap=0.3, priority=2),
    "Throw":        S("B:whoosh_swish_small_0*", 0.45, pitch=0.85, gap=0.05),
    "ShieldUp":     S("M:sfx_shield_up.wav", 0.45, gap=0.1),
    "ShieldBlock":  S("M:sfx_shield_block.wav", 0.4, jitter=0.08, gap=0.08, voices=2),
    "ShieldBreak":  S("M:sfx_shield_break.wav", 0.6, gap=0.1),
    "Points":       S("M:sfx_points.wav", 0.22, jitter=0.02, gap=0.07, voices=2, priority=0),
    "Purchase":     S("M:sfx_purchase.wav", 0.6, jitter=0.0, gap=0.1),
    "PowerUp":      S("B:powerup_whiz_nightvision_goggles_on_01.wav", 0.5, gap=0.1),
    "StickyThunk":  S("B:punch_slap_whack_hit_0*", 0.4, gap=0.05),
    "FuseHiss":     S("B:fuse_stop_burn_out_bomb_dynamite_0*", 0.3, max_length=1.5, gap=0.2, voices=2),

    # Wonder weapons
    "EmberSpinLoop": S("M:sfx_minigun_spin_loop.wav", 0.35, jitter=0.0),
    "EmberShot":    S("G:gun_machinegun_auto_heavy_shot_0[1-8].wav", 0.2, jitter=0.06, gap=0.03, voices=4),
    "FrostCannon":  S("B:sci-fi_weapon_blaster_laser_boom_heavy_0*", 0.5, pitch=1.3, gap=0.1),
    "GaleHorn":     S("M:sfx_gale_horn.wav", 0.6, gap=0.1),
    "SingularityShot": S("B:sci-fi_weapon_blaster_laser_boom_heavy_0*", 0.55, pitch=0.7, gap=0.1),
    "ThunderMaul":  S("S:heavy-thunder-sound-effect-no-copyright-338980-[[]AudioTrimmer.com[]].mp3", 0.75, jitter=0.08, max_length=2.0, gap=0.1, priority=2),

    # Match flow & announcer
    "MatchStart":   S("M:sfx_match_start.wav", 0.6, jitter=0.0, gap=1.0, priority=2),
    "Victory":      S("M:sfx_victory.wav", 0.7, jitter=0.0, gap=1.0, priority=2),
    "Defeat":       S("M:sfx_defeat.wav", 0.7, jitter=0.0, gap=1.0, priority=2),
    "WaveStart":    S("M:sfx_wave_start.wav", 0.7, jitter=0.0, gap=1.0, priority=2),
    "KillingSpree": S("B:announcer_voice_classic_FPS_style_killingspree.wav", 0.85, jitter=0.0, gap=1.0, voices=1, priority=2),
    "Headshot":     S("B:announcer_voice_classic_FPS_style_headshot.wav", 0.85, jitter=0.0, gap=1.0, voices=1, priority=2),

    # UI
    "UiMove":       S("M:sfx_ui_move.wav", 0.35, jitter=0.02, gap=0.03, voices=3),
    "UiConfirm":    S("M:sfx_ui_confirm.wav", 0.4, jitter=0.0, gap=0.05),
    "UiBack":       S("M:sfx_ui_back.wav", 0.4, jitter=0.0, gap=0.05),
    "UiReady":      S("M:sfx_ui_ready.wav", 0.5, jitter=0.0, gap=0.05),
    "UiError":      S("M:sfx_ui_error.wav", 0.4, jitter=0.0, gap=0.1),
    "UiPause":      S("M:sfx_ui_pause.wav", 0.45, jitter=0.0, gap=0.05, priority=2),
    "UiUnpause":    S("M:sfx_ui_unpause.wav", 0.45, jitter=0.0, gap=0.05, priority=2),
    "UiLaunch":     S("M:sfx_crown.wav", 0.6, jitter=0.0, gap=0.5, priority=2),
    "UiEquip":      S(["G:gun_rifle_grab_pickup_01.wav", "G:gun_shotgun_pickup_0*"], 0.45, gap=0.05),
}

# WeaponClass: None 0, Sniper 1, Rifle 2, SMG 3, Shotgun 4, Heavy 5, Launcher 6
GUN_FAMILIES = [
    (0, dict(draw="G:gun_pistol_slide_fast_0*", dryFire="G:gun_pistol_dry_fire_0*", reloadDone="G:gun_pistol_slide_slow_0*", slugLoad=None)),
    (1, dict(draw="G:gun_rifle_sniper_cock_0*", dryFire="G:gun_rifle_sniper_dry_fire_0*", reloadDone="G:gun_rifle_sniper_cock_0*", slugLoad=None)),
    (2, dict(draw="G:gun_semi_auto_rifle_cock_0*", dryFire="G:gun_semi_auto_rifle_dry_fire_0*", reloadDone="G:gun_semi_auto_rifle_cock_0*", slugLoad=None)),
    (3, dict(draw="G:gun_submachine_auto_cock_0*", dryFire="G:gun_submachine_auto_dry_fire_0*", reloadDone="G:gun_submachine_auto_cock_0*", slugLoad=None)),
    (4, dict(draw="G:gun_shotgun_pickup_0*", dryFire="G:gun_shotgun_dry_fire_0*", reloadDone="G:gun_shotgun_cock_0*", slugLoad="G:gun_shotgun_safety_switch_0*")),
    (5, dict(draw="G:gun_rifle_grab_pickup_01.wav", dryFire="G:gun_rifle_dry_fire_0*", reloadDone="G:gun_rifle_cock_0*", slugLoad=None)),
    (6, dict(draw="G:gun_shotgun_pickup_0*", dryFire="G:gun_revolver_pistol_dry_fire_0*", reloadDone="G:gun_revolver_pistol_cylinder_close_0*", slugLoad=None)),
]


def clip_list(paths, indent):
    if not paths:
        return " []\n"
    return "\n" + "".join(f"{indent}- {{fileID: 8300000, guid: {guid(p)}, type: 3}}\n" for p in paths)


def main():
    ids = enum_values()
    missing = [k for k in ids if k != "None" and k not in SOUNDS_TABLE]
    unknown = [k for k in SOUNDS_TABLE if k not in ids]
    if unknown:
        sys.exit(f"not in SfxId: {unknown}")
    if missing:
        print(f"warning: no clips for {missing}")

    script_guid = guid(os.path.join(ROOT, "Assets", "Scripts", "Audio", "SfxBank.cs"))
    y = [
        "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n",
        "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n",
        "  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n",
        f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}\n",
        "  m_Name: SfxBank\n  m_EditorClassIdentifier:\n",
        "  masterVolume: 0.8\n  stereoSpread: 0.45\n  sounds:\n",
    ]
    for name, e in SOUNDS_TABLE.items():
        y.append(f"  - id: {ids[name]}\n    clips:{clip_list(e['clips'], '    ')}")
        y.append(f"    volume: {e['volume']}\n    pitch: {e['pitch']}\n    pitchJitter: {e['jitter']}\n")
        y.append(f"    maxLength: {e['max_length']}\n    minGap: {e['gap']}\n    maxVoices: {e['voices']}\n    priority: {e['priority']}\n")
    y.append("  gunHandlingVolume: 0.35\n  gunFamilies:\n")
    for cls, fam in GUN_FAMILIES:
        y.append(f"  - weaponClass: {cls}\n")
        for key in ("draw", "dryFire", "reloadDone", "slugLoad"):
            paths = clips(fam[key]) if fam[key] else []
            y.append(f"    {key}:{clip_list(paths, '    ')}")
    with open(OUT, "w") as f:
        f.write("".join(y))
    if not os.path.exists(OUT + ".meta"):
        import hashlib
        with open(OUT + ".meta", "w") as f:
            f.write("fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n"
                    "  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
                    % hashlib.md5(b"magnumancer-sfx/SfxBank.asset").hexdigest())
    print(f"wrote {os.path.relpath(OUT, ROOT)}: {len(SOUNDS_TABLE)} sounds, {len(GUN_FAMILIES)} gun families")


if __name__ == "__main__":
    main()
