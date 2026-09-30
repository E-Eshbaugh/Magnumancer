using System.Collections.Generic;
using UnityEngine;

/// A crackling line of lightning left by a Stormrunner blink. Shocks and briefly stuns
/// each enemy that touches it (once per fence).
public class LightningFence : MonoBehaviour
{
    public float lifetime = 1.3f;
    public float touchRadius = 0.8f;
    public float damage = 10f;

    GameObject owner;
    Vector3 a, b;
    Color color, core;
    float born, nextJag;
    LineRenderer bolt1, bolt2, bolt3;
    readonly HashSet<GameObject> shocked = new();

    public void Init(GameObject o, Vector3 from, Vector3 to, Color c)
    {
        owner = o;
        a = from + Vector3.up * 0.9f;
        b = to + Vector3.up * 0.9f;
        color = c;
        core = Color.Lerp(c, Color.white, 0.7f);
        born = Time.time;
        bolt1 = GlowLine.Make(transform, "Fence", 12, 0.22f, AbilityKit.Glow());
        bolt2 = GlowLine.Make(transform, "Fence", 12, 0.1f, AbilityKit.Glow());
        bolt3 = GlowLine.Make(transform, "FenceLow", 10, 0.08f, AbilityKit.Glow());
    }

    void Update()
    {
        float age = Time.time - born;
        if (age >= lifetime) { Destroy(gameObject); return; }
        float fade = 1f - Mathf.Clamp01((age - lifetime * 0.6f) / (lifetime * 0.4f));

        if (Time.time >= nextJag)
        {
            nextJag = Time.time + 0.05f;
            GlowLine.Bolt(bolt1, a, b, 0.3f);
            GlowLine.Bolt(bolt2, a, b, 0.35f);
            GlowLine.Bolt(bolt3, a - Vector3.up * 0.6f, b - Vector3.up * 0.6f, 0.25f);
            if (Random.value < 0.6f)
                PowerFx.Sparks(Vector3.Lerp(a, b, Random.value) - Vector3.up * 0.8f, color, 3, 2.5f, 0.3f, 0.05f, 1f, Vector3.up, 80f);
        }
        GlowLine.SetColor(bolt1, color, 0.8f * fade * Random.Range(0.7f, 1f));
        GlowLine.SetColor(bolt2, core, fade);
        GlowLine.SetColor(bolt3, color, 0.6f * fade);

        foreach (var e in AbilityKit.Enemies((a + b) * 0.5f, Vector3.Distance(a, b) * 0.5f + touchRadius + 1f, owner))
        {
            if (shocked.Contains(e)) continue;
            Vector3 p = AbilityKit.Chest(e);
            if (DistanceToSegment(p, a, b) > touchRadius + 0.5f) continue;
            shocked.Add(e);
            DamageEvents.Deal(e, damage, owner);
            StatusEffects.Of(e).Stun(0.35f, 0.5f);
            AbilityKit.Zap(ClosestOnSegment(p, a, b), p, core, 0.15f, 0.15f);
            PowerFx.Sparks(p, core, 16, 5f, 0.4f, 0.06f, 1f);
            PowerFx.Flash(p, color, 4f, 3f, 0.2f);
        }
    }

    static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = ab.sqrMagnitude > 1e-5f ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
        return a + ab * t;
    }

    static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 c = ClosestOnSegment(p, a, b);
        c.y = p.y = 0f;
        return Vector3.Distance(p, c);
    }
}
