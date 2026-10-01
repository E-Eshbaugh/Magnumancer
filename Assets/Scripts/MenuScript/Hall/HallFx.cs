using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Visual helpers for the Great Hall: render-only copies of gun prefabs (no gameplay
/// scripts), wizard robes in a rune shade, glowing rings and lines.
/// </summary>
public static class HallFx
{
    // ---------- display copies ----------

    /// A copy of `prefab` with only its meshes (no colliders or scripts), wrapped so its
    /// visual centre sits at the wrapper's origin and its barrel points along +X, scaled so
    /// its longest side is `length`. Guns barrel out of their "firePoint"; otherwise the
    /// longest side counts as the barrel.
    public static Transform GunDisplay(GameObject prefab, float length, Transform parent)
    {
        // built at the origin, then parented, so world space == wrapper space throughout
        var wrap = new GameObject(prefab != null ? $"Display {prefab.name}" : "Display").transform;
        if (prefab == null) { wrap.SetParent(parent, false); return wrap; }

        var inner = new GameObject("Model").transform;
        inner.SetParent(wrap, false);
        Transform muzzle = null;
        CopyRenderers(prefab.transform, inner, true, ref muzzle);

        var b = BoundsOf(inner);
        if (b.size == Vector3.zero) { wrap.SetParent(parent, false); return wrap; }

        // barrel = the box axis pointing most toward the muzzle (else the longest side)
        Vector3 barrel;
        if (muzzle != null && (muzzle.position - b.center).sqrMagnitude > 0.0001f)
            barrel = Snap(muzzle.position - b.center);
        else
        {
            var s = b.size;
            barrel = s.x >= s.y && s.x >= s.z ? Vector3.right : s.z >= s.y ? Vector3.forward : Vector3.up;
        }
        Vector3 up = Mathf.Abs(barrel.y) < 0.5f ? Vector3.up : Vector3.forward;
        // rotate so barrel -> +X and up -> +Y (LookRotation(barrel x up, up) maps X to barrel)
        var from = Quaternion.LookRotation(Vector3.Cross(barrel, up), up);
        var fix = Quaternion.Inverse(from);
        inner.localRotation = fix * inner.localRotation;
        inner.localPosition = fix * (inner.localPosition - b.center);

        b = BoundsOf(inner);
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        float k = longest > 0.0001f ? length / longest : 1f;
        inner.localPosition = (inner.localPosition - b.center) * k;
        inner.localScale *= k;
        wrap.SetParent(parent, false);
        return wrap;
    }

    static Vector3 Snap(Vector3 v)
    {
        var a = new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
        if (a.x >= a.y && a.x >= a.z) return new Vector3(Mathf.Sign(v.x), 0, 0);
        if (a.z >= a.y) return new Vector3(0, 0, Mathf.Sign(v.z));
        return new Vector3(0, Mathf.Sign(v.y), 0);
    }

    public static Bounds BoundsOf(Transform root)
    {
        var rends = root.GetComponentsInChildren<Renderer>();
        if (rends.Length == 0) return new Bounds(root.position, Vector3.zero);
        var b = rends[0].bounds;
        foreach (var r in rends) b.Encapsulate(r.bounds);
        return b;
    }

    static void CopyRenderers(Transform src, Transform dst, bool root, ref Transform muzzle)
    {
        if (!root)
        {
            dst.localPosition = src.localPosition;
            dst.localRotation = src.localRotation;
            dst.localScale = src.localScale;
        }
        else
        {
            dst.localRotation = src.localRotation;
            dst.localScale = src.localScale;
        }
        if (src.name.ToLowerInvariant().Contains("firepoint") || src.name.ToLowerInvariant().Contains("muzzle")) muzzle = dst;

        var mf = src.GetComponent<MeshFilter>();
        var mr = src.GetComponent<MeshRenderer>();
        if (mf != null && mr != null && mf.sharedMesh != null)
        {
            dst.gameObject.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            var r = dst.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterials = mr.sharedMaterials;
        }
        var smr = src.GetComponent<SkinnedMeshRenderer>();
        if (smr != null && smr.sharedMesh != null)
        {
            dst.gameObject.AddComponent<MeshFilter>().sharedMesh = smr.sharedMesh;
            dst.gameObject.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
        }
        foreach (Transform c in src)
        {
            if (c.GetComponent<ParticleSystem>() != null) continue;
            var child = new GameObject(c.name).transform;
            child.SetParent(dst, false);
            CopyRenderers(c, child, false, ref muzzle);
        }
    }

    // ---------- wizard robes ----------

    static readonly Dictionary<(WizardData, int), Material> robes = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { robes.Clear(); ringMat = null; }

    /// The wizard's body material in rune shade `shade` (same recolor the arena uses)
    public static Material Robe(WizardData wizard, int shade)
    {
        if (wizard == null || wizard.material == null) return null;
        if (robes.TryGetValue((wizard, shade), out var m) && m != null) return m;
        m = new Material(wizard.material) { name = $"{wizard.wizardName} shade {shade}" };
        if (shade > 0) m.mainTexture = WizardShade.ModelTexture(wizard, shade, m.mainTexture);
        robes[(wizard, shade)] = m;
        return m;
    }

    public static void Dress(GameObject model, WizardData wizard, int shade)
    {
        var m = Robe(wizard, shade);
        if (m == null || model == null) return;
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            if (!(r is SkinnedMeshRenderer) && !(r is MeshRenderer)) continue;
            var mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++) mats[i] = m;
            r.sharedMaterials = mats;
        }
    }

    // ---------- glow ----------

    static Material ringMat;
    public static Material RingMaterial() => ringMat != null ? ringMat : (ringMat = GlowLine.CreateMaterial(2.2f));

    public static LineRenderer Ring(Transform parent, string name, float radius, float width, int points = 48)
    {
        var lr = GlowLine.Make(parent, name, points, width, RingMaterial());
        lr.loop = true;
        lr.useWorldSpace = false;
        for (int i = 0; i < points; i++)
        {
            float a = i / (float)points * Mathf.PI * 2f;
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        return lr;
    }

    /// Ring with notches every `ticks` points: reads as a rune circle
    public static LineRenderer RuneRing(Transform parent, string name, float radius, float width, int ticks)
    {
        int per = 6;
        int n = ticks * per;
        var lr = GlowLine.Make(parent, name, n, width, RingMaterial());
        lr.loop = true;
        lr.useWorldSpace = false;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            float r = radius * (i % per == 0 ? 0.9f : 1f);
            lr.SetPosition(i, new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
        }
        return lr;
    }

    public static Light PointLight(Transform parent, string name, Vector3 localPos, Color color, float intensity, float range)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        return l;
    }
}

/// <summary>Gentle float: bobs and (optionally) turns a display object.</summary>
public class HallBob : MonoBehaviour
{
    public float amplitude = 0.08f;
    public float speed = 1.4f;
    public float spin;          // degrees per second around world up
    public float phase;
    public Vector3 basePos;
    public bool useBase;

    void Start()
    {
        if (!useBase) basePos = transform.localPosition;
        if (phase == 0f) phase = Random.value * 10f;
    }

    void Update()
    {
        transform.localPosition = basePos + Vector3.up * Mathf.Sin((Time.time + phase) * speed) * amplitude;
        if (spin != 0f) transform.Rotate(Vector3.up, spin * Time.deltaTime, Space.World);
    }
}
