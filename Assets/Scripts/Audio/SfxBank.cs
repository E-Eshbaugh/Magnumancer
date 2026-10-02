using UnityEngine;

/// <summary>
/// Which clips every sound effect plays, and how loud. The one in use is
/// Resources/SfxBank.asset: swap clips and tune volumes there, nothing in code needs to
/// change. Gun fire/reload clips stay on each WeaponData; this holds the handling sounds
/// around them (draw, dry fire, rack) per weapon family.
/// </summary>
[CreateAssetMenu(menuName = "Magnumancer/Sfx Bank", fileName = "SfxBank")]
public class SfxBank : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public SfxId id;
        public AudioClip[] clips;           // one picked at random (never the same one twice running)
        [Range(0f, 1f)] public float volume = 0.6f;
        public float pitch = 1f;
        [Tooltip("± random pitch, so repeats don't sound robotic")]
        [Range(0f, 0.5f)] public float pitchJitter = 0.05f;
        [Tooltip("Seconds of the clip to play (0 = all of it)")]
        public float maxLength;
        [Tooltip("The same sound won't retrigger faster than this (seconds)")]
        public float minGap = 0.03f;
        [Tooltip("Most copies of this sound playing at once")]
        [Min(1)] public int maxVoices = 4;
        [Tooltip("When voices run out, quieter priorities get cut first (0 = filler, 2 = must play)")]
        [Range(0, 2)] public int priority = 1;
    }

    [System.Serializable]
    public class GunFamily
    {
        public WeaponClass weaponClass;
        public AudioClip[] draw;
        public AudioClip[] dryFire;
        public AudioClip[] reloadDone;
        public AudioClip[] slugLoad;
    }

    [Range(0f, 1f)] public float masterVolume = 0.8f;
    [Tooltip("How far sounds pan toward the side of the screen they happen on (0 = centered)")]
    [Range(0f, 1f)] public float stereoSpread = 0.45f;
    public Entry[] sounds;

    [Header("Gun handling")]
    [Range(0f, 1f)] public float gunHandlingVolume = 0.35f;
    public GunFamily[] gunFamilies;

    public Entry Find(SfxId id)
    {
        if (sounds == null) return null;
        foreach (var e in sounds) if (e != null && e.id == id) return e;
        return null;
    }

    public AudioClip[] GunClips(WeaponClass cls, GunSfx kind)
    {
        GunFamily match = null, fallback = null;
        if (gunFamilies != null)
            foreach (var f in gunFamilies)
            {
                if (f == null) continue;
                if (f.weaponClass == cls) match = f;
                if (f.weaponClass == WeaponClass.Rifle) fallback = f;
            }
        var fam = match ?? fallback;
        if (fam == null) return null;
        return kind switch
        {
            GunSfx.Draw => fam.draw,
            GunSfx.DryFire => fam.dryFire,
            GunSfx.ReloadDone => fam.reloadDone,
            GunSfx.SlugLoad => fam.slugLoad,
            _ => null,
        };
    }
}
