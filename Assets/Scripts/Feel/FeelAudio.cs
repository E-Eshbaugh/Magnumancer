using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 2D one-shots for hit feel and the announcer. Its own voices, so a flurry of hits never
/// cuts off a reaction sound (ReactionAudio has its own pool). Announcer lines queue up
/// and play one at a time, ducking the music while they talk.
/// </summary>
public static class FeelAudio
{
    const int Voices = 6;

    static FeelSoundBank bank;
    static bool loaded;
    static AudioSource[] voices;
    static AudioSource announcer;
    static int next;
    static readonly Dictionary<string, AudioClip> lines = new();
    static readonly Queue<(AudioClip clip, float at, bool important)> queue = new();
    static AudioClip killChime;

    public static FeelSoundBank Bank
    {
        get
        {
            if (!loaded) { loaded = true; bank = Resources.Load<FeelSoundBank>("FeelSounds"); }
            return bank;
        }
    }

    /// True while the announcer is talking (the music ducks)
    public static bool Speaking => announcer != null && announcer.isPlaying;

    public static void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
    {
        if (clip == null) return;
        Ensure();
        var s = voices[next];
        next = (next + 1) % Voices;
        s.Stop();
        s.clip = clip;
        s.volume = volume;
        s.pitch = pitch;
        s.Play();
    }

    public static void PlayRandom(AudioClip[] clips, float volume, float pitch)
    {
        if (clips == null || clips.Length == 0) return;
        Play(clips[Random.Range(0, clips.Length)], volume, pitch);
    }

    /// The bright two-note chime when you take someone out
    public static void KillChime(float pitch = 1f)
    {
        if (killChime == null) killChime = SynthClips.KillChime();
        Play(killChime, 0.5f, pitch);
    }

    /// Queues an announcer line from Resources/Announcer. Unimportant lines that wait too
    /// long behind others are dropped instead of arriving late.
    public static void Announce(string key, bool important = false)
    {
        var clip = Line(key);
        if (clip == null) return;
        Ensure();
        queue.Enqueue((clip, Time.unscaledTime, important));
    }

    public static bool HasLine(string key) => Line(key) != null;

    static AudioClip Line(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        if (!lines.TryGetValue(key, out var clip))
            lines[key] = clip = Resources.Load<AudioClip>("Announcer/" + key);
        return clip;
    }

    /// Drops lines that haven't been said yet (a new round starting)
    public static void ClearAnnouncer()
    {
        queue.Clear();
        if (announcer != null) announcer.Stop();
    }

    static void Ensure()
    {
        if (voices != null && voices[0] != null) return;
        var go = new GameObject("[FeelAudio]");
        Object.DontDestroyOnLoad(go);
        voices = new AudioSource[Voices];
        for (int i = 0; i < Voices; i++) voices[i] = Source(go);
        announcer = Source(go);
        announcer.priority = 0;
        go.AddComponent<FeelAudioRunner>();
    }

    static AudioSource Source(GameObject go)
    {
        var s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.spatialBlend = 0f;
        s.ignoreListenerPause = true;
        return s;
    }

    internal static void Tick()
    {
        if (announcer == null || announcer.isPlaying) return;
        while (queue.Count > 0)
        {
            var (clip, at, important) = queue.Dequeue();
            if (!important && Time.unscaledTime - at > 2.5f) continue;
            announcer.clip = clip;
            announcer.volume = Bank != null ? Bank.announcerVolume : 1f;
            announcer.pitch = 1f;
            announcer.Play();
            break;
        }
    }

    internal static void SceneChanged() => queue.Clear();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        bank = null;
        loaded = false;
        voices = null;
        announcer = null;
        next = 0;
        lines.Clear();
        queue.Clear();
        killChime = null;
    }
}
