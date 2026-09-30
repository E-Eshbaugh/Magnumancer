using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scars that stay on the arena for the match: big blasts leave a scorched crater
/// with a rim of half-buried rubble, earth magic splits the ground into cracks. Purely
/// visual (no colliders, no navmesh changes). Capped; the oldest fade out.
/// </summary>
public static class Craters
{
    /// Blasts at least this strong leave a crater (fireball 35, grenades, barrels, Combust)
    public static float MinBlastDamage = 26f;   // Combust 26+, fireball 35, barrels; not Brand ignite (25), a flash of flame
    public static int MaxScars = 40;

    static readonly Queue<GameObject> scars = new();
    static Material decalMat;
    static Mesh quad;

    /// A scorched pit with a rubble rim
    public static void Blast(Vector3 at, float radius)
    {
        if (!GroundAt(at, out Vector3 point, out Vector3 normal)) return;
        radius = Mathf.Clamp(radius, 0.6f, 4f);
        var root = NewScar("Crater", point, normal);

        // soot, then the darker pit in the middle
        Decal(root.transform, radius * 2.3f, new Color(0.05f, 0.04f, 0.035f, 0.55f), 0.012f);
        Decal(root.transform, radius * 1.3f, new Color(0.02f, 0.015f, 0.01f, 0.8f), 0.018f);

        // chunks of the floor heaved up around the edge
        int rocks = Mathf.RoundToInt(Mathf.Lerp(5f, 12f, radius / 4f));
        var stone = RockDebris.StoneMaterial(default);
        for (int i = 0; i < rocks; i++)
        {
            float a = (i + Random.value * 0.6f) / rocks * Mathf.PI * 2f;
            Vector3 local = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius * Random.Range(0.75f, 1.05f);
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(rock.GetComponent<Collider>());
            rock.name = "Rubble";
            rock.transform.SetParent(root.transform, false);
            float s = Random.Range(0.12f, 0.3f) * Mathf.Sqrt(radius);
            rock.transform.localPosition = local + Vector3.up * s * 0.15f;   // half sunk
            rock.transform.localRotation = Quaternion.LookRotation(local) * Quaternion.Euler(Random.Range(-40f, -10f), Random.Range(-20f, 20f), Random.Range(-25f, 25f));
            rock.transform.localScale = new Vector3(s * Random.Range(0.8f, 1.4f), s * Random.Range(0.5f, 0.9f), s * Random.Range(0.8f, 1.4f));
            var r = rock.GetComponent<MeshRenderer>();
            r.sharedMaterial = stone;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }

    /// The ground split open (Seismic Judgement)
    public static void Cracks(Vector3 at, float radius)
    {
        if (!GroundAt(at, out Vector3 point, out Vector3 normal)) return;
        var root = NewScar("Cracks", point, normal);
        Decal(root.transform, radius * 0.8f, new Color(0.03f, 0.025f, 0.02f, 0.7f), 0.012f);
        int lines = Random.Range(6, 10);
        for (int i = 0; i < lines; i++)
        {
            float ang = i * 360f / lines + Random.Range(-15f, 15f);
            float len = radius * Random.Range(0.5f, 1f);
            // a crack is a few thin dark strips, each kinked a little
            Vector3 from = Vector3.zero;
            float heading = ang;
            for (int seg = 0; seg < 3; seg++)
            {
                float segLen = len / 3f;
                heading += Random.Range(-25f, 25f);
                Vector3 dir = Quaternion.Euler(0f, heading, 0f) * Vector3.forward;
                var strip = Strip(root.transform, from + dir * segLen * 0.5f, heading, segLen, Mathf.Lerp(0.14f, 0.05f, seg / 2f),
                                  new Color(0.02f, 0.015f, 0.01f, 0.85f));
                from += dir * segLen;
            }
        }
    }

    // ---------- building blocks ----------

    static bool GroundAt(Vector3 at, out Vector3 point, out Vector3 normal)
    {
        point = at; normal = Vector3.up;
        var hits = Physics.RaycastAll(at + Vector3.up * 2f, Vector3.down, 6f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        bool found = false;
        foreach (var h in hits)
        {
            if (h.distance >= best) continue;
            var root = DamageEvents.RootOf(h.collider);
            if (DamageEvents.IsCombatant(root) || h.collider.GetComponentInParent<Destructible>() != null) continue;
            if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;
            best = h.distance; point = h.point; normal = h.normal; found = true;
        }
        return found && normal.y > 0.6f;   // floors, not walls or steep slopes
    }

    static GameObject NewScar(string name, Vector3 point, Vector3 normal)
    {
        var root = new GameObject(name);
        root.transform.SetPositionAndRotation(point, Quaternion.FromToRotation(Vector3.up, normal) * Quaternion.Euler(0f, Random.value * 360f, 0f));
        root.AddComponent<ScarFade>();
        scars.Enqueue(root);
        while (scars.Count > MaxScars)
        {
            var old = scars.Dequeue();
            if (old != null) old.GetComponent<ScarFade>().FadeOut();
        }
        return root;
    }

    static void Decal(Transform parent, float size, Color color, float lift)
    {
        var go = new GameObject("Decal");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.up * lift;
        go.transform.localRotation = Quaternion.Euler(90f, Random.value * 360f, 0f);
        go.transform.localScale = new Vector3(size * Random.Range(0.9f, 1.1f), size * Random.Range(0.8f, 1f), 1f);
        Paint(go, color);
    }

    static GameObject Strip(Transform parent, Vector3 center, float heading, float length, float width, Color color)
    {
        var go = new GameObject("Crack");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center + Vector3.up * 0.02f;
        go.transform.localRotation = Quaternion.Euler(90f, heading, 0f);
        go.transform.localScale = new Vector3(width, length, 1f);
        Paint(go, color);
        return go;
    }

    static void Paint(GameObject go, Color color)
    {
        if (quad == null)
        {
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tmp);
        }
        if (decalMat == null)
        {
            decalMat = GlowLine.CreateParticleMaterial(1f, additive: false);
            decalMat.name = "ScarDecal";
            decalMat.renderQueue = 2450;   // after the floor, before characters and effects
        }
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = decalMat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        PowerFx.Tint(r, color);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => scars.Clear();
}

/// Lets an old scar fade out instead of popping
[AddComponentMenu("")]
class ScarFade : MonoBehaviour
{
    public void FadeOut() => StartCoroutine(Fade());

    System.Collections.IEnumerator Fade()
    {
        Vector3 start = transform.localScale;
        for (float t = 0f; t < 1f; t += Time.deltaTime)
        {
            transform.localScale = new Vector3(start.x, start.y * (1f - t), start.z) * Mathf.Lerp(1f, 0.6f, t);
            yield return null;
        }
        Destroy(gameObject);
    }
}
