using UnityEngine;

/// <summary>
/// Short sounds rendered in code once and kept as clips: the kill chime and the victory
/// fanfare. No asset needed; a real clip in FeelSounds wins over these.
/// </summary>
public static class SynthClips
{
    const int Rate = 44100;

    /// Two bright bell notes a fifth apart: "got 'em"
    public static AudioClip KillChime()
    {
        float len = 0.55f;
        var data = new float[(int)(Rate * len)];
        Bell(data, 0f, Midi(84), 0.5f, 0.45f);
        Bell(data, 0.075f, Midi(91), 0.55f, 0.5f);
        return Make("KillChime", data);
    }

    /// A brassy rising fanfare that lands on a big major chord
    public static AudioClip VictoryFanfare()
    {
        float len = 3.6f;
        var data = new float[(int)(Rate * len)];
        // pickup: G-C-E triplet, then the hit
        Brass(data, 0.00f, 0.16f, new[] { 67 }, 0.5f);
        Brass(data, 0.17f, 0.16f, new[] { 72 }, 0.5f);
        Brass(data, 0.34f, 0.16f, new[] { 76 }, 0.5f);
        Brass(data, 0.52f, 0.5f, new[] { 60, 67, 72, 79 }, 0.55f);
        Brass(data, 1.05f, 0.22f, new[] { 58, 65, 70, 77 }, 0.5f);
        Brass(data, 1.30f, 0.22f, new[] { 62, 69, 74, 81 }, 0.5f);
        Brass(data, 1.55f, 1.9f, new[] { 48, 60, 64, 67, 72, 76 }, 0.6f);
        // cymbal swell under the last chord
        var rng = new System.Random(7);
        int start = (int)(Rate * 1.55f);
        for (int i = start; i < data.Length; i++)
        {
            float t = (i - start) / (float)Rate;
            data[i] += ((float)rng.NextDouble() * 2f - 1f) * 0.12f * Mathf.Exp(-t * 1.6f);
        }
        Timpani(data, 0.52f); Timpani(data, 1.55f);
        Normalize(data, 0.85f);
        return Make("VictoryFanfare", data);
    }

    /// A low, unresolved sting for a draw
    public static AudioClip DrawSting()
    {
        float len = 2.2f;
        var data = new float[(int)(Rate * len)];
        Brass(data, 0f, 0.35f, new[] { 57, 60, 63 }, 0.5f);
        Brass(data, 0.4f, 1.6f, new[] { 56, 59, 62 }, 0.5f);
        Timpani(data, 0f);
        Normalize(data, 0.7f);
        return Make("DrawSting", data);
    }

    static void Bell(float[] data, float at, float freq, float amp, float decay)
    {
        int start = (int)(at * Rate);
        for (int i = start; i < data.Length; i++)
        {
            float t = (i - start) / (float)Rate;
            float env = Mathf.Exp(-t / decay * 4f) * Mathf.Min(1f, t * 400f);
            float s = Mathf.Sin(2f * Mathf.PI * freq * t)
                    + 0.5f * Mathf.Sin(2f * Mathf.PI * freq * 2.76f * t) * Mathf.Exp(-t * 18f)
                    + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 5.4f * t) * Mathf.Exp(-t * 30f);
            data[i] += s * env * amp * 0.5f;
        }
    }

    static void Brass(float[] data, float at, float dur, int[] notes, float amp)
    {
        int start = (int)(at * Rate);
        int end = Mathf.Min(data.Length, start + (int)((dur + 0.35f) * Rate));
        float lp = 0f;
        for (int i = start; i < end; i++)
        {
            float t = (i - start) / (float)Rate;
            float env = Mathf.Min(1f, t / 0.03f) * (t < dur ? 1f : Mathf.Exp(-(t - dur) * 9f));
            float bright = 0.25f + 0.5f * Mathf.Min(1f, t / 0.08f) * (t < dur ? 1f : Mathf.Exp(-(t - dur) * 6f));
            float s = 0f;
            foreach (int n in notes)
            {
                float f = Midi(n) * (1f + 0.002f * Mathf.Sin(t * 30f));
                s += Saw(f * t) + 0.6f * Saw(f * 1.004f * t);
            }
            s /= notes.Length;
            lp += (s - lp) * bright;   // the filter opening is the "blat"
            data[i] += lp * env * amp;
        }
    }

    static void Timpani(float[] data, float at)
    {
        int start = (int)(at * Rate);
        for (int i = start; i < data.Length; i++)
        {
            float t = (i - start) / (float)Rate;
            if (t > 1.2f) break;
            float f = 55f + 40f * Mathf.Exp(-t * 20f);
            data[i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 3.5f) * 0.6f;
        }
    }

    static float Saw(float phase) => 2f * (phase - Mathf.Floor(phase + 0.5f));
    public static float Midi(int n) => 440f * Mathf.Pow(2f, (n - 69) / 12f);

    static void Normalize(float[] data, float peak)
    {
        float max = 0f;
        foreach (var v in data) max = Mathf.Max(max, Mathf.Abs(v));
        if (max <= 0f) return;
        float g = peak / max;
        for (int i = 0; i < data.Length; i++) data[i] *= g;
    }

    static AudioClip Make(string name, float[] data)
    {
        var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
