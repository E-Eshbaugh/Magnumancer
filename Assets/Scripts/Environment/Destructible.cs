using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// What a prop is made of: how it breaks, what its debris looks like, and which
/// elements it's weak or resistant to.
public enum PropMaterial { Stone, Wood, Plant, Crystal, Ice, Metal, Bone }

/// <summary>
/// An environment object that can be shot and blown apart: crates, pillars, statues,
/// mushrooms, trees, interior walls. Bullets and explosions damage it (no health bar:
/// its state shows on the prop itself). It darkens as it weakens and cracks at half health. At zero it crumbles (or, if explosive, blows up),
/// so cover wears away as the match goes on. Walls and big props first break down to a
/// low stub (still half cover) before they go completely.
///
/// Elements: fire sets wood and plants burning (damage over time, it can spread, and a
/// burning prop is a fire zone that can be put out with water); frost makes stone and
/// crystal brittle (x1.5 damage for a few seconds).
///
/// Add it to a prop by hand, or let DestructibleSetup add it at scene load from name
/// rules. It goes on the object that has the collider(s) and renderer(s).
/// </summary>
public class Destructible : MonoBehaviour, IElementZone
{
    public float maxHealth = 120f;
    public PropMaterial material = PropMaterial.Stone;

    [Header("Explosive (barrels, volatile crystals)")]
    [Tooltip("0 = crumbles quietly; above 0 = blows up for this much damage")]
    public float explodeDamage;
    public float explodeRadius = 3.5f;

    [Header("Stub")]
    [Tooltip("First 'death' knocks it down to a low stub that's still cover, second one clears it")]
    public bool leavesStub;
    [Range(0.1f, 0.8f)] public float stubHeight = 0.35f;
    [Tooltip("Stub health, as a fraction of max")]
    [Range(0.1f, 1f)] public float stubHealth = 0.4f;

    [Tooltip("Size of the debris burst (auto from the prop's size when 0)")]
    public float debrisScale;
    [Tooltip("Debris color (alpha 0 = a default for the material)")]
    public Color debrisColor = new Color(0f, 0f, 0f, 0f);

    // ---------- tuning shared by all props ----------
    public static float BurnDps = 8f;           // what burning does to the prop itself
    public static float BurnTime = 4f;          // refreshed by more fire
    public static float SpreadRadius = 1.5f;    // flames jump to flammable props this close...
    public static float SpreadChance = 0.3f;    // ...with this chance each second
    public static float BrittleTime = 4f;
    public static float BrittleMultiplier = 1.5f;

    public float Health { get; private set; }
    public bool IsDestroyed { get; private set; }
    public bool IsStub { get; private set; }
    public bool IsBurning => Time.time < burningUntil;
    public bool IsBrittle => Time.time < brittleUntil;
    public float HealthFraction => maxHealth > 0f ? Mathf.Clamp01(Health / maxHealth) : 0f;
    public float LastHitTime { get; private set; } = -999f;

    /// The prop's world bounds (for debris and explosions)
    public Bounds Bounds { get; private set; }

    public bool Flammable => material == PropMaterial.Wood || material == PropMaterial.Plant;

    /// prop, who destroyed it (null for blasts nobody owns)
    public static event Action<Destructible, GameObject> Destroyed;

    public static readonly List<Destructible> All = new();

    Renderer[] renderers;
    Collider[] colliders;
    Color[] baseColors;
    MaterialPropertyBlock block;
    Vector3 restPosition;
    Coroutine jolt, burn;
    bool batched;       // static-batched meshes can't be moved or scaled at runtime
    bool cracked;       // the half-health crack has happened
    float burningUntil, brittleUntil, charred;
    GameObject burnedBy;

    void Awake()
    {
        Health = maxHealth;
        renderers = GetComponentsInChildren<Renderer>();
        colliders = GetComponentsInChildren<Collider>();
        Bounds = ComputeBounds();
        foreach (var r in renderers) if (r != null && r.isPartOfStaticBatch) { batched = true; break; }
        if (debrisScale <= 0f) debrisScale = Mathf.Clamp(Bounds.size.magnitude * 0.35f, 0.4f, 3f);

        block = new MaterialPropertyBlock();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i] != null ? renderers[i].sharedMaterial : null;
            baseColors[i] = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                          : m != null && m.HasProperty("_Color") ? m.color : Color.white;
        }
    }

    void OnEnable() => All.Add(this);

    void OnDisable()
    {
        All.Remove(this);
        ElementZones.Unregister(this);
    }

    /// Reset max health (DestructibleSetup sizes props)
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
            if (c.isTrigger || !c.enabled) continue;
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
        => Damage(amount, attacker, point, element, false);

    void Damage(float amount, GameObject attacker, Vector3 point, Element element, bool quiet)
    {
        if (IsDestroyed || amount <= 0f) return;
        float mult = ElementMultiplier(material, element);
        if (IsBrittle && element != Element.Frost) mult *= BrittleMultiplier;
        Health -= amount * mult;
        LastHitTime = Time.time;

        // elements leave their mark on the prop
        if (element == Element.Fire && Flammable) Ignite(attacker);
        if (element == Element.Frost && (material == PropMaterial.Stone || material == PropMaterial.Crystal)) MakeBrittle();

        if (!quiet)
        {
            Chips(point, Mathf.Clamp(amount / 15f, 0.5f, 2f) * (mult > 1f ? 1.5f : 1f));
            if (!batched)
            {
                if (jolt != null) StopCoroutine(jolt);
                jolt = StartCoroutine(Jolt(Mathf.Clamp(amount / 60f, 0.02f, 0.12f)));
            }
        }

        if (Health <= 0f)
        {
            if (leavesStub && !IsStub && !batched) BecomeStub();
            else Break(attacker);
        }
        else if (!cracked && HealthFraction <= 0.5f) Crack();
        UpdateLook();
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

    // ---------- damage states ----------

    // Half health: it visibly gives, a burst of dust and chips and a slight lean
    void Crack()
    {
        cracked = true;
        Vector3 c = Bounds.center;
        RockDebris.Dust(new Vector3(c.x, Bounds.min.y, c.z), Mathf.Max(0.5f, debrisScale * 0.6f), 5);
        Chips(c + Vector3.up * Bounds.extents.y * 0.5f, 1.5f);
        if (!batched)
        {
            if (jolt != null) { StopCoroutine(jolt); jolt = null; }
            if (restPosition != Vector3.zero) transform.position = restPosition;
            transform.rotation = Quaternion.AngleAxis(UnityEngine.Random.Range(2f, 4f), UnityEngine.Random.onUnitSphere) * transform.rotation;
        }
    }

    // Darker as it weakens, charred as it burns, icy while brittle
    void UpdateLook()
    {
        if (renderers == null) return;
        float wear = 1f - HealthFraction;
        for (int i = 0; i < renderers.Length; i++)
        {
            var r = renderers[i];
            if (r == null) continue;
            Color c = baseColors[i] * Mathf.Lerp(1f, 0.55f, wear * wear);
            c = Color.Lerp(c, new Color(0.12f, 0.09f, 0.07f), charred * 0.7f);
            if (IsBrittle) c = Color.Lerp(c, new Color(0.75f, 0.9f, 1f), 0.4f);
            c.a = baseColors[i].a;
            r.GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            r.SetPropertyBlock(block);
        }
    }

    // ---------- fire and frost ----------

    public void Ignite(GameObject by)
    {
        if (IsDestroyed || !Flammable) return;
        burnedBy = by;
        bool wasBurning = IsBurning;
        burningUntil = Time.time + BurnTime;
        if (!wasBurning)
        {
            ElementZones.Register(this);   // a burning prop is a fire zone: gas and brambles nearby go up
            if (burn == null) burn = StartCoroutine(Burn());
        }
    }

    IEnumerator Burn()
    {
        Color fire = Elements.ColorOf(Element.Fire);
        float nextTick = Time.time + 0.5f, nextSpread = Time.time + 1f;
        while (IsBurning && !IsDestroyed)
        {
            // flames licking up the prop
            var b = Bounds;
            Vector3 p = new Vector3(b.center.x, b.min.y, b.center.z)
                      + Vector3.Scale(new Vector3(UnityEngine.Random.value - 0.5f, UnityEngine.Random.value, UnityEngine.Random.value - 0.5f), b.size * 0.9f);
            BulletFX.Mote(BulletFX.Flavor.Embers, fire, p, 1.4f);
            if (UnityEngine.Random.value < 0.15f)
                PowerFx.Puffs(p, new Color(0.15f, 0.12f, 0.1f, 1f), 1, 0.8f, 0.6f, 1f, lift: 1.5f);

            if (Time.time >= nextTick)
            {
                nextTick = Time.time + 0.5f;
                charred = Mathf.Min(1f, charred + 0.06f);
                Damage(BurnDps * 0.5f, burnedBy, b.center, Element.None, true);
            }
            if (Time.time >= nextSpread)
            {
                nextSpread = Time.time + 1f;
                Spread();
            }
            yield return null;
        }
        burn = null;
        ElementZones.Unregister(this);
    }

    void Spread()
    {
        foreach (var other in All)
        {
            if (other == this || !other.Flammable || other.IsBurning || other.IsDestroyed) continue;
            if (other.Bounds.SqrDistance(Bounds.center) > (Bounds.extents.magnitude + SpreadRadius) * (Bounds.extents.magnitude + SpreadRadius)) continue;
            if (UnityEngine.Random.value < SpreadChance) other.Ignite(burnedBy);
        }
    }

    void MakeBrittle()
    {
        bool was = IsBrittle;
        brittleUntil = Time.time + BrittleTime;
        if (!was)
        {
            PowerFx.Sparks(Bounds.center, new Color(0.75f, 0.9f, 1f), 12, 3f, 0.4f, 0.05f, 0.3f);
            StartCoroutine(ThawLater());
        }
    }

    IEnumerator ThawLater()
    {
        while (IsBrittle) yield return null;
        UpdateLook();
    }

    // ---------- IElementZone (only registered while burning) ----------
    public Element ZoneElement => Element.Fire;
    public GameObject ZoneOwner => burnedBy;
    public Vector3 ZoneCenter => new Vector3(Bounds.center.x, Bounds.min.y, Bounds.center.z);
    public float ZoneRadius => Mathf.Max(Bounds.extents.x, Bounds.extents.z) + 0.5f;
    public float DistanceTo(Vector3 p)
    {
        Vector3 c = Bounds.ClosestPoint(new Vector3(p.x, Bounds.center.y, p.z));
        c.y = p.y = 0f;
        return Vector3.Distance(c, p);
    }
    /// Water hit it (Steam): the fire's out
    public void Consume()
    {
        burningUntil = 0f;
        ElementZones.Unregister(this);
    }

    // ---------- destruction ----------

    // Walls and big props: the top comes off, the base stays as low cover
    void BecomeStub()
    {
        IsStub = true;
        Health = maxHealth * stubHealth;
        cracked = true;
        if (jolt != null) { StopCoroutine(jolt); jolt = null; }
        if (restPosition != Vector3.zero) transform.position = restPosition;

        var before = Bounds;
        Color col = DebrisColor();
        var mat = DebrisMaterial();
        int chunks = Mathf.RoundToInt(Mathf.Lerp(8f, 18f, Mathf.InverseLerp(0.4f, 3f, debrisScale)));
        for (int i = 0; i < chunks; i++)
        {
            // the part above the stub line is what breaks off
            Vector3 p = new Vector3(before.center.x, before.min.y + before.size.y * Mathf.Lerp(stubHeight, 1f, UnityEngine.Random.value), before.center.z)
                      + Vector3.Scale(UnityEngine.Random.insideUnitSphere, new Vector3(before.extents.x, 0f, before.extents.z) * 0.8f);
            Vector3 v = (p - before.center).normalized * UnityEngine.Random.Range(1.5f, 4f) + Vector3.up * UnityEngine.Random.Range(1f, 4f);
            RockDebris.Chunk(p, v, UnityEngine.Random.Range(0.12f, 0.28f) * debrisScale, UnityEngine.Random.Range(1f, 1.8f), mat, col);
        }
        RockDebris.Dust(new Vector3(before.center.x, before.min.y, before.center.z), Mathf.Max(0.8f, debrisScale), 8);
        Rumble.Blast(before.center, Mathf.Max(3f, debrisScale * 3f), 0.3f);
        CameraShake.Shake(0.05f + 0.03f * debrisScale, 0.15f);

        // squash it down, keeping its base on the ground, with a broken tilt
        var s = transform.localScale;
        transform.localScale = new Vector3(s.x, s.y * stubHeight, s.z);
        transform.rotation = Quaternion.AngleAxis(UnityEngine.Random.Range(-4f, 4f), transform.right) * transform.rotation;
        Physics.SyncTransforms();
        var after = ComputeBounds();
        transform.position += Vector3.up * (before.min.y - after.min.y);
        Physics.SyncTransforms();
        restPosition = transform.position;
        Bounds = ComputeBounds();
        UpdateLook();
    }

    void Break(GameObject attacker)
    {
        IsDestroyed = true;
        Health = 0f;
        burningUntil = 0f;
        ElementZones.Unregister(this);
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
        if (charred > 0.2f)
            PowerFx.Puffs(center, new Color(0.1f, 0.08f, 0.07f, 1f), 8, 2f, 1.2f, 1.2f, lift: 1.5f);   // burnt out

        Rumble.Blast(center, Mathf.Max(3f, debrisScale * 3f), 0.35f);
        CameraShake.Shake(0.05f + 0.04f * debrisScale, 0.15f);

        if (explodeDamage > 0f) Explode(attacker, center, ground);

        Destroyed?.Invoke(this, attacker);
        if (batched) gameObject.SetActive(false);   // can't slump a batched mesh: gone under the debris
        else StartCoroutine(Sink());
    }

    // Barrels and volatile crystals: a real explosion that hurts, shoves, sets off mines,
    // grenades and other explosive props (chains), and sets wood nearby on fire
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
        foreach (var other in All.ToArray())
            if (other != this && other.Flammable && other.Bounds.SqrDistance(center) <= explodeRadius * explodeRadius)
                other.Ignite(attacker);

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
