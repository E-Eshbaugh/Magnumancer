using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sounds for reactions and combo callouts. Clips live in a ReactionSoundBank asset at
/// Resources/ReactionSounds (swap clips there; nothing here needs to change). Plays 2D:
/// it's one shared screen, and a reaction anywhere should be heard.
/// </summary>
public static class ReactionAudio
{
    const int Voices = 8;
    const float MinGap = 0.08f;   // the same reaction a frame apart doesn't double up

    static ReactionSoundBank bank;
    static bool loaded;
    static AudioSource[] voices;
    static int next;
    static readonly Dictionary<Reaction, float> lastPlayed = new();

    static ReactionSoundBank Bank()
    {
        if (!loaded)
        {
            loaded = true;
            bank = Resources.Load<ReactionSoundBank>("ReactionSounds");
        }
        return bank;
    }

    public static void Play(Reaction reaction, bool echo = false)
    {
        var b = Bank();
        var e = b != null ? b.Find(reaction) : null;
        if (e == null || e.clips == null || e.clips.Length == 0) return;
        if (lastPlayed.TryGetValue(reaction, out float last) && Time.time - last < MinGap) return;
        lastPlayed[reaction] = Time.time;

        // an Echo is the same sound, deeper and a touch quieter
        float pitch = e.pitch * (echo ? 0.75f : 1f) * Random.Range(0.95f, 1.05f);
        Voice(e.clips[Random.Range(0, e.clips.Length)], e.volume * (echo ? 0.8f : 1f), pitch, e.maxLength);
    }

    /// Combo callouts: the announcer as a streak reaches 4 and 5
    public static void Combo(int count)
    {
        var b = Bank();
        if (b == null) return;
        var clip = count == 5 ? b.comboOverload : count == 4 ? b.comboFrenzy : null;   // once each, not every reaction after
        if (clip != null) Voice(clip, b.announcerVolume, 1f, 0f);
    }

    /// The sound bank (item drop clips live there too)
    public static ReactionSoundBank Sounds => Bank();

    /// Plays any clip through the shared 2D voices (scaled by the bank's master volume)
    public static void PlayClip(AudioClip clip, float volume = 1f, float pitch = 1f, float maxLength = 0f)
        => Voice(clip, volume, pitch, maxLength);

    static void Voice(AudioClip clip, float volume, float pitch, float maxLength)
    {
        if (clip == null) return;
        if (voices == null || voices[0] == null)
        {
            var go = new GameObject("[ReactionAudio]");
            voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                voices[i] = go.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].spatialBlend = 0f;
            }
        }
        var s = voices[next];
        next = (next + 1) % Voices;
        s.Stop();
        s.clip = clip;
        s.volume = volume * (Bank() != null ? Bank().masterVolume : 1f);
        s.pitch = pitch;
        s.Play();
        // long clips (a burning fuse) are cut short to the moment we want
        if (maxLength > 0f)
            s.SetScheduledEndTime(AudioSettings.dspTime + maxLength / Mathf.Max(0.1f, pitch));
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        bank = null;
        loaded = false;
        voices = null;
        next = 0;
        lastPlayed.Clear();
    }
}
