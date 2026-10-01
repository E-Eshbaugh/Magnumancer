using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The active rune gives a wizard its own shade, so two players on the same wizard can
/// tell each other apart (and read which rune the other is running):
///  • Rune I: the wizard's own color;
///  • Rune II: hue nudged one way, a little softer;
///  • Rune III: hue nudged the other way, pale.
/// The hue nudge is sized per wizard so it never drifts toward another wizard's color (at
/// most a third of the gap to the nearest neighbor hue). The passive rune doesn't count.
/// If two players on the same wizard pick the same active rune, the later one gets the
/// free shade that looks most different.
///
/// Lives on the player; every per-player visual (bullets, bars, trails, auras, abilities
/// via AbilityKit.Theme, spawn and death effects) reads WizardShade.Of(player), and the
/// wizard model itself wears a recolored palette texture (ModelTexture, via PlayerAppearance).
/// Menus show the wizard's own color.
/// </summary>
public class WizardShade : MonoBehaviour
{
    public const int Count = 3;
    /// Largest hue nudge in degrees, even for wizards with lots of room
    public static float MaxHueShift = 14f;
    /// Saturation of Rune II and III shades, relative to the wizard's own
    public static float Rune2Saturation = 0.7f, Rune3Saturation = 0.4f;

    public WizardData wizard;
    public int shade;
    public Color color;   // raw theme color in this shade (callers Brighten as before)

    /// A player's color: their shade if they have one, else their wizard's own
    public static Color Of(GameObject player)
    {
        var s = player != null ? player.GetComponentInParent<WizardShade>() : null;
        return s != null ? s.color : WizardSpawnEffect.ThemeColorOf(AbilityKit.Wizard(player));
    }

    /// A player's shade (0 = the wizard's own color)
    public static int IndexOfPlayer(GameObject player)
    {
        var s = player != null ? player.GetComponentInParent<WizardShade>() : null;
        return s != null ? s.shade : 0;
    }

    /// `wizard`'s theme color in shade i (i = active rune index)
    public static Color Shade(WizardData wizard, int i)
    {
        Color c = WizardSpawnEffect.ThemeColorOf(wizard);
        i = Mathf.Clamp(i, 0, Count - 1);
        if (i == 0 || wizard == null) return c;

        Color.RGBToHSV(c, out float h, out float s, out float v);
        s = Mathf.Min(s, 0.85f);   // the same cap Brighten applies, so the steps survive it
        Room(wizard, out float up, out float down);
        if (i == 1) { h += up / 360f; s *= Rune2Saturation; }
        else { h -= down / 360f; s *= Rune3Saturation; }

        var r = Color.HSVToRGB(Mathf.Repeat(h, 1f), s, v);
        r.a = 1f;
        return r;
    }

    // How far this wizard's hue can move each way before it starts looking like a neighbor
    static readonly Dictionary<WizardData, Vector2> room = new();
    static void Room(WizardData wizard, out float up, out float down)
    {
        if (!room.TryGetValue(wizard, out var r))
        {
            Color.RGBToHSV(WizardSpawnEffect.ThemeColorOf(wizard), out float h, out _, out _);
            float gapUp = 360f, gapDown = 360f;
            foreach (var other in Resources.LoadAll<WizardData>("Wizards"))
            {
                if (other == null || other.passive == wizard.passive) continue;
                Color.RGBToHSV(WizardSpawnEffect.ThemeColorOf(other), out float oh, out _, out _);
                float d = Mathf.Repeat((oh - h) * 360f, 360f);   // degrees going up to reach it
                gapUp = Mathf.Min(gapUp, d);
                gapDown = Mathf.Min(gapDown, 360f - d);
            }
            r = new Vector2(Mathf.Min(MaxHueShift, gapUp / 3f), Mathf.Min(MaxHueShift, gapDown / 3f));
            room[wizard] = r;
        }
        up = r.x; down = r.y;
    }

    /// How different two shades look on screen (after the usual Brighten)
    static float Difference(WizardData wizard, int a, int b)
    {
        Color x = GlowLine.Brighten(Shade(wizard, a)), y = GlowLine.Brighten(Shade(wizard, b));
        return Mathf.Sqrt((x.r - y.r) * (x.r - y.r) + (x.g - y.g) * (x.g - y.g) + (x.b - y.b) * (x.b - y.b));
    }

    /// Puts the shade of their active rune on `player`. If an earlier player on the same
    /// wizard already has it (`taken`), they get the free shade that looks most different
    /// from the taken ones (with 4 of one wizard, someone has to double up).
    public static WizardShade Apply(GameObject player, WizardData wizard, int activeRune, ICollection<int> taken)
    {
        int pick = Mathf.Clamp(activeRune, 0, Count - 1);
        if (wizard != null && taken != null && taken.Contains(pick))
        {
            float best = -1f;
            for (int i = 0; i < Count; i++)
            {
                if (taken.Contains(i)) continue;
                float closest = float.MaxValue;
                foreach (int t in taken) closest = Mathf.Min(closest, Difference(wizard, i, t));
                if (closest > best) { best = closest; pick = i; }
            }
        }

        var s = player.GetComponent<WizardShade>();
        if (s == null) s = player.AddComponent<WizardShade>();
        s.wizard = wizard;
        s.shade = pick;
        s.color = Shade(wizard, pick);
        return s;
    }

    // ---------- the wizard model ----------

    /// Resolution of recolored model textures (the KayKit palettes are big flat swatches)
    const int ModelTextureSize = 512;
    static readonly Dictionary<(Texture, WizardData, int), Texture2D> modelTextures = new();

    /// The player whose model `part` belongs to: its shade (0 = the wizard's own colors)
    public static int IndexOfModel(GameObject part)
    {
        if (part == null) return 0;
        var s = part.GetComponentInParent<WizardShade>();
        if (s == null) s = part.transform.root.GetComponentInChildren<WizardShade>();
        return s != null ? s.shade : 0;
    }

    /// `source` (a wizard's palette texture) recolored for shade i: only the swatches in the
    /// wizard's own color family move, by the same hue nudge and saturation step as the
    /// shade itself, so skin, metal and wood stay put. Cached per texture/wizard/shade.
    public static Texture ModelTexture(WizardData wizard, int shade, Texture source)
    {
        if (source == null || wizard == null || shade <= 0) return source;
        var key = (source, wizard, shade);
        if (modelTextures.TryGetValue(key, out var cached) && cached != null) return cached;

        Color.RGBToHSV(WizardSpawnEffect.ThemeColorOf(wizard), out float th, out float ts, out _);
        Color.RGBToHSV(Shade(wizard, shade), out float sh, out float ss, out _);
        float dh = Mathf.DeltaAngle(th * 360f, sh * 360f) / 360f;
        float sMul = ss / Mathf.Max(0.01f, Mathf.Min(ts, 0.85f));
        float dv = shade == 2 ? 0.12f : -0.06f;   // the pale shade a touch lighter, the soft one a touch deeper
        // what counts as "colored" scales with the wizard's own saturation (Frostwarden is
        // pastel); the floor sits just above the palettes' skin tones (~0.33) so faces stay put
        float satLo = Mathf.Min(0.38f, ts * 0.6f), satHi = Mathf.Min(0.6f, ts * 0.9f);

        // the source isn't CPU-readable: copy it through the GPU
        int w = Mathf.Min(ModelTextureSize, source.width), h = Mathf.Min(ModelTextureSize, source.height);
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Graphics.Blit(source, rt);
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
        {
            name = $"{source.name}_shade{shade}",
            wrapMode = source.wrapMode,
            filterMode = source.filterMode,
        };
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        var px = tex.GetPixels32();
        for (int i = 0; i < px.Length; i++)
        {
            Color c = px[i];
            Color.RGBToHSV(c, out float ph, out float ps, out float pv);
            float near = 1f - Mathf.InverseLerp(25f, 60f, Mathf.Abs(Mathf.DeltaAngle(ph * 360f, th * 360f)));
            float k = near * Mathf.InverseLerp(satLo, satHi, ps);
            if (k <= 0.001f) continue;
            Color o = Color.HSVToRGB(Mathf.Repeat(ph + dh * k, 1f), ps * Mathf.Lerp(1f, sMul, k), Mathf.Clamp01(pv + dv * k));
            o.a = c.a;
            px[i] = o;
        }
        tex.SetPixels32(px);
        tex.Apply(true, true);
        modelTextures[key] = tex;
        return tex;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { room.Clear(); modelTextures.Clear(); }
}
