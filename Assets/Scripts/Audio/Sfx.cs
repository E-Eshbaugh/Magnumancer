using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the game's sound effects. Clips and volumes live in Resources/SfxBank (see SfxBank).
///
/// Everything plays 2D (one shared couch screen: you should hear the fight wherever it
/// is), panned a little toward the side of the screen it happens on. A fixed pool of
/// voices, per-sound retrigger gaps and voice caps keep four players and a horde from
/// turning into mush: when the pool is full, the least important, oldest sound is cut.
///
///   Sfx.Play(SfxId.KillConfirm);              // centered
///   Sfx.Play(SfxId.Explosion, blastCenter);   // panned to where it happened
///   Sfx.StartLoop(this, SfxId.EmberSpinLoop); Sfx.SetLoop(this, volume, pitch); Sfx.StopLoop(this);
/// </summary>
public static class Sfx
{
    const int Voices = 24;

    class Voice
    {
        public AudioSource source;
        public SfxId id;
        public int priority;
        public float startedAt;
    }

    static SfxBank bank;
    static bool loaded;
    static GameObject host;
    static Voice[] voices;
    static readonly Dictionary<SfxId, float> lastPlayed = new();
    static readonly Dictionary<SfxId, int> lastClip = new();
    static readonly Dictionary<Object, AudioSource> loops = new();
    static readonly List<Object> deadLoops = new();
    static Camera cam;

    public static SfxBank Bank
    {
        get
        {
            if (!loaded)
            {
                loaded = true;
                bank = Resources.Load<SfxBank>("SfxBank");
                if (bank == null) Debug.LogWarning("Sfx: Resources/SfxBank.asset not found; game sounds are off.");
            }
            return bank;
        }
    }

    /// Master scale for every effect (ReactionAudio and gun shots have their own)
    public static float MasterVolume => Bank != null ? Bank.masterVolume : 0f;

    /// Plays a sound centered on screen
    public static void Play(SfxId id, float volume = 1f, float pitch = 1f)
        => Play(id, null, volume, pitch);

    /// Plays a sound panned toward where it happened
    public static void Play(SfxId id, Vector3 at, float volume = 1f, float pitch = 1f)
        => Play(id, (Vector3?)at, volume, pitch);

    static void Play(SfxId id, Vector3? at, float volume, float pitch)
    {
        var b = Bank;
        var e = b != null ? b.Find(id) : null;
        if (e == null || e.clips == null || e.clips.Length == 0 || volume <= 0f) return;

        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float last) && now - last < e.minGap) return;
        if (CountPlaying(id) >= e.maxVoices && !StealOldest(id)) return;
        lastPlayed[id] = now;

        var clip = Pick(id, e.clips);
        float p = e.pitch * pitch * (1f + Random.Range(-e.pitchJitter, e.pitchJitter));
        Start(clip, id, e.priority, e.volume * volume, p, e.maxLength, at);
    }

    /// Plays any clip through the shared voices (scaled by master volume)
    public static void PlayClip(AudioClip clip, Vector3? at = null, float volume = 1f, float pitch = 1f,
                                float maxLength = 0f, int priority = 1)
    {
        if (clip == null || Bank == null) return;
        Start(clip, SfxId.None, priority, volume, pitch, maxLength, at);
    }

    /// A weapon family's handling sound (draw, dry fire, rack, slug load)
    public static void Gun(WeaponClass cls, GunSfx kind, Vector3? at = null, float volume = 1f)
    {
        var b = Bank;
        var clips = b != null ? b.GunClips(cls, kind) : null;
        if (clips == null || clips.Length == 0) return;
        var clip = clips[Random.Range(0, clips.Length)];
        Start(clip, SfxId.None, 1, b.gunHandlingVolume * volume, Random.Range(0.96f, 1.04f), 0f, at);
    }

    // ---------- Deferred (explosions under a reaction) ----------

    struct Late { public SfxId id; public Vector3 at; public float volume, pitch; }
    static readonly List<Late> late = new();

    /// Plays at the end of this frame, unless an elemental reaction sounded this frame (a
    /// Combust's own boom already covers the blast it sets off)
    public static void PlayLate(SfxId id, Vector3 at, float volume = 1f, float pitch = 1f)
        => late.Add(new Late { id = id, at = at, volume = volume, pitch = pitch });

    /// End of frame, called by SfxDirector: plays the deferred sounds
    public static void FlushLate()
    {
        if (late.Count == 0) return;
        bool reaction = ReactionAudio.LastPlayFrame == Time.frameCount;
        foreach (var l in late)
            if (!reaction) Play(l.id, l.at, l.volume, l.pitch);
        late.Clear();
    }

    // ---------- Loops (a spinning minigun, a hum) ----------

    /// Starts (or keeps) a looping sound owned by `owner`; it stops on its own if the owner is destroyed
    public static void StartLoop(Object owner, SfxId id, float volume = 1f, float pitch = 1f)
    {
        if (owner == null) return;
        var e = Bank != null ? Bank.Find(id) : null;
        if (e == null || e.clips == null || e.clips.Length == 0) return;
        if (!loops.TryGetValue(owner, out var src) || src == null)
        {
            EnsureHost();
            src = host.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            src.clip = e.clips[0];
            src.priority = 64;
            loops[owner] = src;
        }
        src.volume = e.volume * volume * MasterVolume;
        src.pitch = e.pitch * pitch;
        if (!src.isPlaying) src.Play();
    }

    public static void SetLoop(Object owner, float volume, float pitch = 1f)
    {
        if (owner == null || !loops.TryGetValue(owner, out var src) || src == null || src.clip == null) return;
        var e = Bank != null ? FindByClip(src.clip) : null;
        src.volume = (e != null ? e.volume : 1f) * volume * MasterVolume;
        src.pitch = (e != null ? e.pitch : 1f) * pitch;
    }

    public static void StopLoop(Object owner)
    {
        if (owner == null || !loops.TryGetValue(owner, out var src)) return;
        loops.Remove(owner);
        if (src != null) Object.Destroy(src);
    }

    /// Housekeeping, called every frame by SfxDirector: drops loops whose owner is gone
    public static void Tick()
    {
        if (loops.Count == 0) return;
        deadLoops.Clear();
        foreach (var kv in loops)
            if (kv.Key == null) deadLoops.Add(kv.Key);   // Unity-null: destroyed owner
        foreach (var k in deadLoops)
        {
            if (loops.TryGetValue(k, out var src) && src != null) Object.Destroy(src);
            loops.Remove(k);
        }
    }

    // ---------- Internals ----------

    static SfxBank.Entry FindByClip(AudioClip clip)
    {
        if (Bank?.sounds == null) return null;
        foreach (var e in Bank.sounds)
            if (e?.clips != null && System.Array.IndexOf(e.clips, clip) >= 0) return e;
        return null;
    }

    static AudioClip Pick(SfxId id, AudioClip[] clips)
    {
        if (clips.Length == 1) return clips[0];
        lastClip.TryGetValue(id, out int prev);
        int i = Random.Range(0, clips.Length - 1);
        if (i >= prev) i++;   // never the same clip twice in a row
        lastClip[id] = i;
        return clips[i];
    }

    static int CountPlaying(SfxId id)
    {
        if (voices == null) return 0;
        int n = 0;
        foreach (var v in voices)
            if (v.source != null && v.id == id && v.source.isPlaying) n++;
        return n;
    }

    static bool StealOldest(SfxId id)
    {
        Voice oldest = null;
        foreach (var v in voices)
            if (v.source != null && v.id == id && v.source.isPlaying && (oldest == null || v.startedAt < oldest.startedAt))
                oldest = v;
        if (oldest == null) return false;
        oldest.source.Stop();
        return true;
    }

    static void Start(AudioClip clip, SfxId id, int priority, float volume, float pitch, float maxLength, Vector3? at)
    {
        if (clip == null) return;
        EnsureHost();
        var v = FreeVoice(priority);
        if (v == null) return;

        var s = v.source;
        s.Stop();
        s.clip = clip;
        s.volume = Mathf.Clamp01(volume * MasterVolume);
        s.pitch = Mathf.Clamp(pitch, 0.1f, 3f);
        s.panStereo = at.HasValue ? Pan(at.Value) : 0f;
        s.priority = 128 - priority * 32;   // lower = more important to Unity's own voice limit
        s.ignoreListenerPause = GamePause.IsPaused;   // pause menu sounds play while the fight is held
        s.Play();
        if (maxLength > 0f)
            s.SetScheduledEndTime(AudioSettings.dspTime + maxLength / Mathf.Max(0.1f, s.pitch));

        v.id = id;
        v.priority = priority;
        v.startedAt = Time.unscaledTime;
    }

    /// A free voice, else the least important one playing (oldest first), if it's no more important than us
    static Voice FreeVoice(int priority)
    {
        Voice pick = null;
        foreach (var v in voices)
        {
            if (v.source == null) continue;
            if (!v.source.isPlaying) return v;
            if (v.priority > priority) continue;
            if (pick == null || v.priority < pick.priority || v.priority == pick.priority && v.startedAt < pick.startedAt)
                pick = v;
        }
        return pick;
    }

    static float Pan(Vector3 world)
    {
        if (cam == null || !cam.isActiveAndEnabled) cam = Camera.main;
        if (cam == null || Bank == null) return 0f;
        Vector3 vp = cam.WorldToViewportPoint(world);
        if (vp.z < 0f) return 0f;
        return Mathf.Clamp((vp.x - 0.5f) * 2f, -1f, 1f) * Bank.stereoSpread;
    }

    static void EnsureHost()
    {
        if (host != null && voices != null && voices[0].source != null) return;
        host = new GameObject("[Sfx]");
        Object.DontDestroyOnLoad(host);
        voices = new Voice[Voices];
        for (int i = 0; i < Voices; i++)
        {
            var s = host.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.dopplerLevel = 0f;
            voices[i] = new Voice { source = s };
        }
        loops.Clear();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        bank = null;
        loaded = false;
        host = null;
        voices = null;
        cam = null;
        lastPlayed.Clear();
        lastClip.Clear();
        loops.Clear();
        late.Clear();
    }
}
