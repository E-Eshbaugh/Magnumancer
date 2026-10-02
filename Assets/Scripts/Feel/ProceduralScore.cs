using UnityEngine;

/// <summary>
/// The built-in score, synthesized live (no music assets needed). Two themes:
///  - Hall: slow D-minor pads, a soft sub bass and music-box bells.
///  - Battle: driving A-minor (Am F Dm E). Layers come in with Intensity:
///    0 = kick, hats, bass, pad   1 = + snare and arpeggio   2 = + lead melody, open hats.
/// MusicDirector drives it. Everything below OnAudioFilterRead runs on the audio thread:
/// no Unity API in there, only the plain fields the main thread sets.
/// </summary>
[RequireComponent(typeof(AudioSource))]
[AddComponentMenu("")]
public class ProceduralScore : MonoBehaviour
{
    public const int None = 0, Hall = 1, Battle = 2;

    // ---- set from the main thread ----
    public volatile int theme;
    public volatile int intensity;
    public volatile int transpose;
    public float gain;          // 0..1, already includes fades and ducking
    volatile bool restart;

    public void Play(int newTheme, int semitones)
    {
        transpose = semitones;
        theme = newTheme;
        restart = true;
    }

    // ---- audio thread state ----
    double sr = 48000;
    long sample;
    int step = -1;
    float curGain;
    float layerSnare, layerArp, layerLead, layerOpenHat;
    uint rng = 0x9E3779B9u;

    // voices
    float kickT = 9f, kickPhase;
    float snareT = 9f, snareHp, snarePhase;
    float hatT = 9f, hatDecay = 60f, hatHp;
    float bassT = 9f, bassFreq, bassPhase, bassLp;
    float arpT = 9f, arpFreq, arpPhase, arpLp, arpPan = 0.5f;
    float leadT = 9f, leadFreq, leadPhase, leadLp, leadGate;
    float bellT = 9f, bellFreq, bellPhase, bellPhase2;
    readonly float[] padFreq = new float[3];
    readonly float[] padPhase = new float[6];
    float padLp, padEnv;
    float chordFade = 1f;

    void Awake() => sr = AudioSettings.outputSampleRate;

    // ---------------- songs ----------------

    struct Song
    {
        public float bpm;
        public int[] bass;        // per bar
        public int[,] triad;      // per bar
        public int[] melody;      // per 16th, -1 = hold
    }

    static readonly Song BattleSong = new Song
    {
        bpm = 140f,
        bass = new[] { 45, 41, 38, 40 },
        triad = new[,] { { 57, 60, 64 }, { 53, 57, 60 }, { 62, 65, 69 }, { 64, 68, 71 } },
        melody = new[]
        {
            76,-1,-1,72, -1,69,-1,-1, 71,72,-1,71, -1,69,-1,-1,
            69,-1,-1,65, -1,69,-1,72, -1,-1,-1,69, -1,-1,-1,-1,
            74,-1,-1,77, -1,76,-1,74, -1,72,-1,-1, 74,-1,-1,-1,
            76,-1,-1,68, -1,71,-1,-1, 76,-1,74,-1, 72,-1,71,-1,
        },
    };

    static readonly Song HallSong = new Song
    {
        bpm = 72f,
        bass = new[] { 38, 34, 41, 36 },
        triad = new[,] { { 62, 65, 69 }, { 62, 65, 70 }, { 60, 65, 69 }, { 60, 64, 67 } },
        melody = new[]
        {
            81,-1,-1,-1, 77,-1,-1,-1, 76,-1,74,-1, -1,-1,-1,-1,
            74,-1,-1,-1, 77,-1,-1,-1, 82,-1,-1,-1, 81,-1,-1,-1,
            81,-1,-1,-1, 77,-1,79,-1, 81,-1,-1,-1, 72,-1,-1,-1,
            76,-1,-1,-1, 74,-1,72,-1, 74,-1,-1,-1, -1,-1,-1,-1,
        },
    };

    // arpeggio: indexes into triad (+3 = root an octave up)
    static readonly int[] ArpPattern = { 0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 4, 3, 2, 1, 2, 3 };

    static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    // ---------------- engine ----------------

    void OnAudioFilterRead(float[] data, int channels)
    {
        int th = theme;
        if (restart) { restart = false; sample = 0; step = -1; chordFade = 0f; }

        float target = gain;
        int inten = intensity;
        // layers glide in and out over about half a second
        float glide = 1f - Mathf.Exp(-(float)(data.Length / channels) / (float)(sr * 0.25));
        layerSnare += ((th == Battle && inten >= 1 ? 1f : 0f) - layerSnare) * glide;
        layerArp += ((inten >= 1 || th == Hall ? 1f : 0f) - layerArp) * glide;
        layerLead += ((th == Battle && inten >= 2 ? 1f : 0f) - layerLead) * glide;
        layerOpenHat += ((th == Battle && inten >= 2 ? 1f : 0f) - layerOpenHat) * glide;

        if (th == None && curGain < 0.0005f)
        {
            System.Array.Clear(data, 0, data.Length);
            return;
        }

        Song song = th == Hall ? HallSong : BattleSong;
        double samplesPerStep = sr * 60.0 / song.bpm / 4.0;
        float dt = (float)(1.0 / sr);
        int frames = data.Length / channels;
        float gStep = 1f / (float)(sr * 0.05);

        for (int f = 0; f < frames; f++)
        {
            int s = (int)(sample / samplesPerStep);
            if (s != step) { step = s; Trigger(th, song, s); }
            sample++;

            curGain += Mathf.Clamp(target - curGain, -gStep, gStep);

            float l, r;
            if (th == Hall) HallSample(dt, out l, out r);
            else BattleSample(dt, out l, out r);

            l = Soft(l * curGain); r = Soft(r * curGain);
            if (channels >= 2)
            {
                data[f * channels] = l;
                data[f * channels + 1] = r;
                for (int c = 2; c < channels; c++) data[f * channels + c] = 0f;
            }
            else data[f] = (l + r) * 0.5f;
        }
    }

    void Trigger(int th, Song song, int s)
    {
        int bar = (s / 16) % 4, pos = s % 16;
        int tr = transpose;

        if (pos == 0)
        {
            for (int i = 0; i < 3; i++) padFreq[i] = Hz(song.triad[bar, i] + tr - (th == Battle ? 12 : 0));
            chordFade = 0f;
        }

        int mel = song.melody[(s % 64)];

        if (th == Hall)
        {
            // bass on the bar and the half; bells on the 8ths through the chord
            if (pos == 0 || pos == 8) { bassT = 0f; bassFreq = Hz(song.bass[bar] + tr); }
            if (pos % 2 == 0)
            {
                int idx = ArpPattern[(pos / 2 + bar * 3) % ArpPattern.Length];
                int note = idx >= 3 ? song.triad[bar, idx - 3] + 12 : song.triad[bar, idx];
                arpT = 0f; arpFreq = Hz(note + tr + 12);
                arpPan = (pos / 2) % 2 == 0 ? 0.3f : 0.7f;
            }
            if (mel >= 0) { bellT = 0f; bellFreq = Hz(mel + tr); }
            return;
        }

        // ---- Battle ----
        // drums: four on the floor, snare 2 & 4 with a fill at the end of the phrase
        if (pos % 4 == 0) { kickT = 0f; kickPhase = 0f; }
        if (bar == 3 && pos == 14) { kickT = 0f; kickPhase = 0f; }
        if (pos == 4 || pos == 12 || (bar == 3 && (pos == 13 || pos == 15))) snareT = 0f;
        if (pos % 2 == 1) { hatT = 0f; hatDecay = (pos == 7 || pos == 15) && layerOpenHat > 0.5f ? 9f : 55f; }
        else if (layerOpenHat > 0.5f) { hatT = 0f; hatDecay = 80f; }

        // bass: driving 8ths, octave jumps on the off-beats
        if (pos % 2 == 0)
        {
            int b = song.bass[bar] + tr;
            bassT = 0f;
            bassFreq = Hz(pos % 4 == 2 ? b + 12 : b);
        }

        // arp: 16ths through the chord
        {
            int idx = ArpPattern[pos];
            int note = idx >= 3 ? song.triad[bar, idx - 3] + 12 : song.triad[bar, idx];
            arpT = 0f; arpFreq = Hz(note + tr + 12);
            arpPan = pos % 2 == 0 ? 0.35f : 0.65f;
        }

        if (mel >= 0) { leadT = 0f; leadFreq = Hz(mel + tr); leadGate = 1f; }
    }

    void BattleSample(float dt, out float l, out float r)
    {
        float mono = 0f;

        // kick: pitch-dropping sine thump
        if (kickT < 0.5f)
        {
            float fk = 45f + 120f * Mathf.Exp(-kickT * 30f);
            kickPhase += fk * dt; kickPhase -= (int)kickPhase;
            mono += Mathf.Sin(2f * Mathf.PI * kickPhase) * Mathf.Exp(-kickT * 9f) * 0.9f;
            kickT += dt;
        }

        // snare: noise burst plus a body tone
        if (snareT < 0.4f && layerSnare > 0.01f)
        {
            float n = Noise();
            float hp = n - snareHp; snareHp = n;
            snarePhase += 185f * dt; snarePhase -= (int)snarePhase;
            mono += (hp * 0.5f * Mathf.Exp(-snareT * 16f) + Mathf.Sin(2f * Mathf.PI * snarePhase) * 0.35f * Mathf.Exp(-snareT * 28f)) * layerSnare;
            snareT += dt;
        }

        // hats
        float hat = 0f;
        if (hatT < 0.5f)
        {
            float n = Noise();
            hat = (n - hatHp) * 0.18f * Mathf.Exp(-hatT * hatDecay);
            hatHp = n;
            hatT += dt;
        }

        // bass: filtered saw with a pluck
        float bass = 0f;
        if (bassT < 1f)
        {
            bassPhase += bassFreq * dt; if (bassPhase >= 2f) bassPhase -= 2f;
            float saw = Saw(bassPhase) + 0.5f * Saw(bassPhase * 0.5f);
            float cut = 0.04f + 0.18f * Mathf.Exp(-bassT * 18f);
            bassLp += (saw - bassLp) * cut;
            bass = bassLp * 0.42f * (0.55f + 0.45f * Mathf.Exp(-bassT * 8f));
            bassT += dt;
        }

        // sidechain-ish pump from the kick
        float pump = 1f - 0.45f * Mathf.Exp(-kickT * 10f);

        // pad: detuned saws, dark
        chordFade = Mathf.Min(1f, chordFade + dt * 6f);
        float pad = 0f;
        for (int i = 0; i < 3; i++)
        {
            padPhase[i * 2] += padFreq[i] * dt;
            padPhase[i * 2 + 1] += padFreq[i] * 1.006f * dt;
            padPhase[i * 2] -= (int)padPhase[i * 2]; padPhase[i * 2 + 1] -= (int)padPhase[i * 2 + 1];
            pad += Saw(padPhase[i * 2]) + Saw(padPhase[i * 2 + 1]);
        }
        padLp += (pad - padLp) * 0.025f;
        pad = padLp * 0.07f * chordFade;

        // arp: pulse with a short pluck
        float arp = 0f;
        if (layerArp > 0.01f && arpT < 0.3f)
        {
            arpPhase += arpFreq * dt; arpPhase -= (int)arpPhase;
            float pulse = Frac(arpPhase) < 0.3f ? 1f : -1f;
            arpLp += (pulse - arpLp) * (0.08f + 0.25f * Mathf.Exp(-arpT * 30f));
            arp = arpLp * 0.13f * Mathf.Exp(-arpT * 12f) * layerArp;
            arpT += dt;
        }

        // lead: saw/square with vibrato
        float lead = 0f;
        if (layerLead > 0.01f)
        {
            float vib = 1f + 0.006f * Mathf.Sin(leadT * 34f) * Mathf.Clamp01(leadT * 4f);
            leadPhase += leadFreq * vib * dt; leadPhase -= (int)leadPhase;
            float wave = 0.6f * Saw(leadPhase) + 0.4f * (Frac(leadPhase) < 0.5f ? 1f : -1f);
            leadLp += (wave - leadLp) * 0.12f;
            float env = Mathf.Min(1f, leadT * 120f) * (0.55f + 0.45f * Mathf.Exp(-leadT * 5f));
            lead = leadLp * 0.14f * env * layerLead;
            leadT += dt;
        }

        mono += (bass + pad) * pump;
        float center = mono + lead + hat * 0.6f;
        l = center + arp * (1f - arpPan) * 2f + hat * 0.4f;
        r = center + arp * arpPan * 2f + hat * 0.2f;
    }

    void HallSample(float dt, out float l, out float r)
    {
        // soft sine sub bass with a slow swell
        float bass = 0f;
        if (bassT < 4f)
        {
            bassPhase += bassFreq * dt; bassPhase -= (int)bassPhase;
            bass = Mathf.Sin(2f * Mathf.PI * bassPhase) * 0.28f * Mathf.Min(1f, bassT * 6f) * Mathf.Exp(-bassT * 0.9f);
            bassT += dt;
        }

        chordFade = Mathf.Min(1f, chordFade + dt * 1.2f);
        float pad = 0f;
        for (int i = 0; i < 3; i++)
        {
            padPhase[i * 2] += padFreq[i] * dt;
            padPhase[i * 2 + 1] += padFreq[i] * 1.004f * dt;
            padPhase[i * 2] -= (int)padPhase[i * 2]; padPhase[i * 2 + 1] -= (int)padPhase[i * 2 + 1];
            pad += Saw(padPhase[i * 2]) + Tri(padPhase[i * 2 + 1]);
        }
        padLp += (pad - padLp) * 0.012f;
        padEnv = 0.75f + 0.25f * Mathf.Sin((float)(sample / sr) * 0.7f);   // slow breathing
        pad = padLp * 0.075f * chordFade * padEnv;

        // music-box arpeggio
        float arp = 0f;
        if (arpT < 1.2f)
        {
            arpPhase += arpFreq * dt; arpPhase -= (int)arpPhase;
            arp = (Mathf.Sin(2f * Mathf.PI * arpPhase) + 0.3f * Mathf.Sin(4f * Mathf.PI * arpPhase) * Mathf.Exp(-arpT * 9f))
                  * 0.07f * Mathf.Exp(-arpT * 3.2f) * Mathf.Min(1f, arpT * 300f);
            arpT += dt;
        }

        // bell melody: inharmonic partials ring out
        float bell = 0f;
        if (bellT < 3f)
        {
            bellPhase += bellFreq * dt; bellPhase -= (int)bellPhase;
            bellPhase2 += bellFreq * 2.76f * dt; bellPhase2 -= (int)bellPhase2;
            bell = (Mathf.Sin(2f * Mathf.PI * bellPhase) + 0.35f * Mathf.Sin(2f * Mathf.PI * bellPhase2) * Mathf.Exp(-bellT * 4f))
                   * 0.09f * Mathf.Exp(-bellT * 1.4f) * Mathf.Min(1f, bellT * 400f);
            bellT += dt;
        }

        float center = bass + pad + bell;
        l = center + arp * (1f - arpPan) * 2f;
        r = center + arp * arpPan * 2f;
    }

    float Noise()
    {
        rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
        return (rng / (float)uint.MaxValue) * 2f - 1f;
    }

    static float Frac(float x) => x - Mathf.Floor(x);
    static float Saw(float phase) => 2f * Frac(phase) - 1f;
    static float Tri(float phase) => 1f - 4f * Mathf.Abs(Frac(phase) - 0.5f);
    static float Soft(float x) => x / (1f + Mathf.Abs(x));
}
