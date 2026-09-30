using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Bright, glowing LineRenderers (laser sights, spawn/dash bolts). Uses URP's unlit
/// particle shader with additive blending and an HDR tint, so lines add light (and
/// catch bloom) instead of being shaded or muddied by the ground behind them. Line
/// colors come from the LineRenderer's vertex colors; alpha fades them out.
/// </summary>
public static class GlowLine
{
    /// New additive material; intensity > 1 over-brightens for bloom. Each user gets
    /// its own so it can animate intensity (SetIntensity).
    /// additive = glowing light; false = ordinary see-through (smoke, mist)
    public static Material CreateMaterial(float intensity = 2f, bool additive = true)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        Material m;
        if (shader != null)
        {
            m = new Material(shader) { name = additive ? "GlowLine" : "GlowSmoke" };
            m.SetFloat("_Surface", 1f);   // transparent
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
        else
        {
            // Fallback: unlit, tinted by vertex color
            m = new Material(Shader.Find("Sprites/Default")) { name = "GlowLine (fallback)" };
        }
        SetIntensity(m, intensity);
        return m;
    }

    public static void SetIntensity(Material m, float intensity)
    {
        if (m == null) return;
        var c = new Color(intensity, intensity, intensity, 1f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    static Texture2D softDot;

    /// Round, soft-edged particle sprite (no texture asset needed)
    public static Texture2D SoftDot()
    {
        if (softDot != null) return softDot;
        const int n = 64;
        softDot = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
        var px = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                a = a * a * (3f - 2f * a); // smoothstep falloff
                px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
        softDot.SetPixels32(px);
        softDot.Apply();
        return softDot;
    }

    /// Particle material with the soft round sprite
    public static Material CreateParticleMaterial(float intensity, bool additive)
    {
        var m = CreateMaterial(intensity, additive);
        var tex = SoftDot();
        if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        return m;
    }

    /// Theme colors can be dark (purple, brown). Keep the hue, push it bright.
    public static Color Brighten(Color c)
    {
        Color.RGBToHSV(c, out float h, out float s, out float v);
        if (v < 0.02f) return Color.white;
        var b = Color.HSVToRGB(h, Mathf.Min(s, 0.85f), 1f);
        b.a = 1f;
        return b;
    }

    /// Mostly-white core of a beam, tinted toward its color
    public static Color Core(Color c) => Color.Lerp(Brighten(c), Color.white, 0.7f);

    public static LineRenderer Make(Transform parent, string name, int points, float width, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        Configure(lr, points, width, mat);
        return lr;
    }

    public static void Configure(LineRenderer lr, int points, float width, Material mat)
    {
        lr.useWorldSpace = true;
        lr.positionCount = points;
        lr.widthMultiplier = 1f;
        lr.startWidth = width;
        lr.endWidth = width;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.alignment = LineAlignment.View;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sharedMaterial = mat;
    }

    public static void SetColor(LineRenderer lr, Color c, float alpha)
    {
        c.a = Mathf.Clamp01(alpha);
        lr.startColor = c;
        lr.endColor = c;
    }

    /// Jagged lightning between a and b (ends pinned), written into lr's positions.
    public static void Bolt(LineRenderer lr, Vector3 a, Vector3 b, float jitter)
    {
        int n = lr.positionCount;
        Vector3 along = b - a;
        Vector3 side = Vector3.Cross(along.sqrMagnitude > 1e-6f ? along.normalized : Vector3.forward, Vector3.up);
        if (side.sqrMagnitude < 1e-4f) side = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        side.Normalize();
        Vector3 up = Vector3.Cross(side, along.normalized);

        for (int i = 0; i < n; i++)
        {
            float f = n > 1 ? i / (float)(n - 1) : 0f;
            Vector3 p = a + along * f;
            if (i > 0 && i < n - 1)
            {
                float j = jitter * Mathf.Sin(f * Mathf.PI);
                p += side * Random.Range(-j, j) + up * Random.Range(-j, j) * 0.6f;
            }
            lr.SetPosition(i, p);
        }
    }
}
