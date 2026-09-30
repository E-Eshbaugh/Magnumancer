using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// What a prop is made of: how it breaks, what its debris looks like, and which
/// elements it's weak or resistant to.
public enum PropMaterial { Stone, Wood, Plant, Crystal, Ice, Metal, Bone }

/// <summary>
/// An environment object that can be shot and blown apart: crates, pillars, statues,
/// mushrooms, ice chunks. Bullets and explosions damage it; a health bar appears over it
/// while it's being hit and fades when left alone; at zero it crumbles (or, if
/// explosive, blows up), so cover wears away as the match goes on.
///
/// Add it to a prop by hand, or let DestructibleSetup add it at scene load from name
/// rules (Resources/DestructibleRules). Everything lives on the object that has the
/// collider(s) and renderer(s) as children.
/// </summary>
public class Destructible : MonoBehaviour
{
    public float maxHealth = 120f;
    public PropMaterial material = PropMaterial.Stone;

    [Header("Explosive (barrels, volatile crystals)")]
    [Tooltip("0 = crumbles quietly; above 0 = blows up for this much damage")]
    public float explodeDamage;
    public float explodeRadius = 3.5f;

    [Tooltip("Size of the debris burst (auto from the prop's size when 0)")]
    public float debrisScale;
    [Tooltip("Debris color (alpha 0 = a default for the material)")]
    public Color debrisColor = new Color(0f, 0f, 0f, 0f);

    public float Health { get; private set; }
    public bool IsDestroyed { get; private set; }
    public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(Health / maxHealth) : 0f;
    public float LastHitTime { get; private set; } = -999f;

    /// The prop's world bounds (for health bar placement, debris, explosions)
    public Bounds Bounds { get; private set; }

    /// prop, who destroyed it (null for blasts nobody owns)
    public static event Action<Destructible, GameObject> Destroyed;

    public static readonly List<Destructible> All = new();

    Renderer[] renderers;
    Collider[] colliders;
    Vector3 restPosition;
    Coroutine jolt;
    bool batched;   // static-batched meshes can't be moved or scaled at runtime

    void Awake()
    {
        Health = maxHealth;
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        Bounds = ComputeBounds();
        foreach (var r in renderers) if (r != null && r.isPartOfStaticBatch) { batched = true; break; }
        if (debrisScale <= 0f) debrisScale = Mathf.Clamp(Bounds.size.magnitude * 0.35f, 0.4f, 3f);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// Reset max health (DestructibleSetup sizes props by volume)
    public void SetMaxHealth(float hp)
    {
        maxHealth = hp;
        Health = hp;
    }

    Bounds ComputeBounds()
    {
        bool any = false;
        var b = new Bounds(transform.position, Vector3.zero);
        foreach (var c in GetComponentsInChildren<Collider>())
        {
            if (c.isTrigger) continue;
            if (!any) { b = c.bounds; any = true; } else b.Encapsulate(c.bounds);
        }
        if (!any)
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
        return b;
    }

    // ---------- damage ----------

    /// How hard an element hits this material
    public static float ElementMultiplier(PropMaterial m, Element e) => (m, e) switch
    {
        (PropMaterial.Wood, Element.Fire) => 2f,
        (PropMaterial.Plant, Element.Fire) => 2f,
        (PropMaterial.Plant, Element.Poison) => 1.5f,
        (PropMaterial.Ice, Element.Fire) => 2f,
        (PropMaterial.Ice, Element.Frost) => 0.5f,
        (PropMaterial.Stone, Element.Earth) => 1.5f,
        (PropMaterial.Crystal, Element.Earth) => 1.5f,
        (PropMaterial.Metal, Element.Lightning) => 1.5f,
        (PropMaterial.Bone, Element.Void) => 1.5f,
        _ => 1f
    };

    public void TakeDamage(float amount, GameObject attacker, Vector3 point, Element element = Element.None)
    {
        if (IsDestroyed || amount <= 0f) return;
        float mult = ElementMultiplier(material, element);
        Health -= amount * mult;
        LastHitTime = Time.time;

        PropHealthBar.Show(this);
        Chips(point, Mathf.Clamp(amount / 15f, 0.5f, 2f) * (mult > 1f ? 1.5f : 1f));
        if (!batched)
        {
            if (jolt != null) StopCoroutine(jolt);
            jolt = StartCoroutine(Jolt(Mathf.Clamp(amount / 60f, 0.02f, 0.12f)));
        }

        if (Health <= 0f) Break(attacker);
    }

    /// Blasts ripple outward: props a little farther out break a beat later
    public void TakeDamageAfter(float delay, float amount, GameObject attacker, Vector3 point, Element element = Element.None)
    {
        if (IsDestroyed) return;
        if (delay <= 0f || !isActiveAndEnabled) { TakeDamage(amount, attacker, point, element); return; }
        StartCoroutine(Delayed(delay, amount, attacker, point, element));
    }

    IEnumerator Delayed(float delay, float amount, GameObject attacker, Vector3 point, Element element)
    {
        yield return new WaitForSeconds(delay);
        TakeDamage(amount, attacker, point, element);
    }

    // A few bits knocked off where it was hit
    void Chips(Vector3 point, float power)
    {
        Color c = DebrisColor();
        int n = Mathf.RoundToInt(2 + 2 * power);
        var mat = DebrisMaterial();
        for (int i = 0; i < n; i++)
        {
            Vector3 away = (point - Bounds.center); away.y = 0f;
            Vector3 v = (away.normalized + UnityEngine.Random.insideUnitSphere * 0.6f + Vector3.up * 0.8f) * UnityEngine.Random.Range(2f, 4f) * power;
            RockDebris.Chunk(point, v, UnityEngine.Random.Range(0.05f, 0.1f) * debrisScale, 0.6f, mat, c);
        }
        if (material == PropMaterial.Crystal || material == PropMaterial.Ice)
            PowerFx.Sparks(point, Color.Lerp(c, Color.white, 0.5f), 6, 3f, 0.25f, 0.05f, 1f);
    }

    // A quick shake so the hit reads on the prop itself
    IEnumerator Jolt(float strength)
    {
        if (restPosition == Vector3.zero) restPosition = transform.position;
        float t = 0f;
        while (t < 0.12f)
        {
            t += Time.deltaTime;
            transform.position = restPosition + UnityEngine.Random.insideUnitSphere * strength * (1f - t / 0.12f);
            yield return null;
        }
        transform.position = restPosition;
        jolt = null;
    }

    // ---------- destruction ----------

    void Break(GameObject attacker)
    {
        IsDestroyed = true;
        Health = 0f;
        if (jolt != null) { StopCoroutine(jolt); jolt = null; }
        if (restPosition != Vector3.zero) transform.position = restPosition;

        // no longer blocks anything: bullets, players, or zombies' paths (Zombies uses carving obstacles)
        foreach (var c in colliders) if (c != null) c.enabled = false;
        foreach (var o in GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>()) o.enabled = false;

        Vector3 center = Bounds.center;
        Vector3 ground = AbilityKit.Ground(center + Vector3.up);
        Color col = DebrisColor();
        var mat = DebrisMaterial();

        // it falls apart: chunks of itself, flung up and out
        int chunks = Mathf.RoundToInt(Mathf.Lerp(8f, 22f, Mathf.InverseLerp(0.4f, 3f, debrisScale)));
        for (int i = 0; i < chunks; i++)
        {
            Vector3 p = center + Vector3.Scale(UnityEngine.Random.insideUnitSphere, Bounds.extents * 0.8f);
            Vector3 v = (p - center).normalized * UnityEngine.Random.Range(1.5f, 4f) + Vector3.up * UnityEngine.Random.Range(2f, 5f);
            RockDebris.Chunk(p, v, UnityEngine.Random.Range(0.12f, 0.3f) * debrisScale, UnityEngine.Random.Range(1f, 1.8f), mat, col);
        }
        RockDebris.Dust(ground, Mathf.Max(0.8f, debrisScale), 6 + chunks / 2);
        if (material == PropMaterial.Crystal || material == PropMaterial.Ice)
        {
            PowerFx.IceShards(center, col, 16, 6f, 0.15f * debrisScale);
            PowerFx.Flash(center, col, 5f, 5f, 0.3f);
        }
        if (material == PropMaterial.Plant)
            for (int i = 0; i < 10; i++)
                BulletFX.Mote(BulletFX.Flavor.Spores, col, center + UnityEngine.Random.insideUnitSphere * debrisScale * 0.5f, 1.8f);

        Rumble.Blast(center, Mathf.Max(3f, debrisScale * 3f), 0.35f);
        CameraShake.Shake(0.05f + 0.04f * debrisScale, 0.15f);

        if (explodeDamage > 0f) Explode(attacker, center, ground);

        Destroyed?.Invoke(this, attacker);
        PropHealthBar.Hide(this);
        if (batched) gameObject.SetActive(false);   // can't slump a batched mesh: gone under the debris
        else StartCoroutine(Sink());
    }

    // Barrels and volatile crystals: a real explosion that hurts, shoves and sets off
    // mines, grenades and other explosive props (chains)
    void Explode(GameObject attacker, Vector3 center, Vector3 ground)
    {
        Color fire = Elements.ColorOf(Element.Fire);
        foreach (var e in AbilityKit.Enemies(center, explodeRadius, null))
        {
            float k = 1f - Mathf.Clamp01(Vector3.Distance(center, e.transform.position) / explodeRadius);
            DamageEvents.Deal(e, explodeDamage * Mathf.Lerp(0.4f, 1f, k), attacker);
        }
        Explosions.AffectWorld(center, explodeRadius, explodeDamage, gameObject);
        ElementReactions.OnElementArea(center, explodeRadius, attacker, Element.Fire);   // gas and brambles go up

        AbilityKit.Shockwave(ground, explodeRadius * 1.2f, fire, 0.4f);
        PowerFx.Flash(center, fire, 12f, explodeRadius * 3f, 0.45f);
        PowerFx.Sparks(center, fire, 50, 10f, 0.8f, 0.1f, 1.2f, Vector3.up, 150f);
        PowerFx.Puffs(center, new Color(0.15f, 0.12f, 0.1f, 1f), 12, 3f, 1.5f, 1.4f, lift: 1.5f);
        CameraShake.Shake(0.3f, 0.3f);
    }

    // The leftovers slump into the ground, then it's gone
    IEnumerator Sink()
    {
        Vector3 start = transform.localScale;
        float t = 0f;
        const float time = 0.35f;
        while (t < time)
        {
            t += Time.deltaTime;
            float k = t / time;
            transform.localScale = new Vector3(start.x * (1f + 0.15f * k), start.y * (1f - k), start.z * (1f + 0.15f * k));
            yield return null;
        }
        gameObject.SetActive(false);
    }

    // ---------- looks ----------

    // Plain lit chunks tinted to the prop. (Not the prop's own material: these models use
    // a palette atlas, so a cube of it comes out multi-colored.)
    static Material DebrisMaterial() => RockDebris.StoneMaterial(default);

    Color DebrisColor()
    {
        if (debrisColor.a > 0f) return debrisColor;
        return material switch
        {
            PropMaterial.Wood => new Color(0.5f, 0.33f, 0.18f),
            PropMaterial.Plant => new Color(0.4f, 0.7f, 0.3f),
            PropMaterial.Crystal => new Color(0.6f, 0.8f, 1f),
            PropMaterial.Ice => new Color(0.75f, 0.9f, 1f),
            PropMaterial.Metal => new Color(0.5f, 0.5f, 0.55f),
            PropMaterial.Bone => new Color(0.85f, 0.82f, 0.72f),
            _ => new Color(0.45f, 0.42f, 0.4f)
        };
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        All.Clear();
        Destroyed = null;
    }
}
