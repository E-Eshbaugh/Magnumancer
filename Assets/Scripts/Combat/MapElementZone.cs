using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Map terrain that carries an element: Drowned Sanctum's shallows (Water), Cinder
/// Crucible's lava (Fire). Put it on an object whose collider covers the area players
/// stand in (the collider can be a trigger or solid; only its bounds/shape are used).
///
/// Anyone standing in it gets the element's status every tick (water Soaks, lava sets
/// Burning), which can set off reactions with no one credited. Reactions reach it too,
/// temporarily: Conduct electrifies water, Brittle freezes it into slippery ice, a
/// Mudslide churns it to mud. Map terrain is never used up.
/// </summary>
public class MapElementZone : MonoBehaviour, IElementZone, IElectrifiable, IFreezable, IMuddable
{
    public Element element = Element.Water;
    [Tooltip("Standing in it applies the element's status (off for e.g. a frozen lake that should only turn slippery)")]
    public bool appliesStatus = true;
    [Tooltip("Players this far above the collider's top still count as standing in it")]
    public float standHeight = 1.2f;
    [Tooltip("Optional: the collider to use (defaults to the one on this object)")]
    public Collider area;

    const float Tick = 0.25f;
    const float ShockTick = 0.5f;
    float nextTick, nextShock, nextCrackle;
    string key;

    // temporary states from reactions
    GameObject electrifiedBy;
    float electrifiedUntil, electrifyDps;
    float iceUntil, iceTraction = 1f;
    float mudUntil, mudSlow = 1f;
    GameObject mudBy;

    readonly HashSet<PlayerMovement3D> sliding = new(), mired = new();

    void Start()
    {
        if (area == null) area = GetComponent<Collider>();
        key = "mapzone" + GetEntityId();
        if (area == null) { Debug.LogWarning($"[MapElementZone] {name} has no collider"); enabled = false; return; }
        ElementZones.Register(this);
    }

    bool Iced => Time.time < iceUntil;
    bool Muddy => Time.time < mudUntil;
    bool Electrified => Time.time < electrifiedUntil;

    bool Contains(Vector3 p)
    {
        var b = area.bounds;
        return p.y >= b.min.y - 0.5f && p.y <= b.max.y + standHeight && ElementZones.FlatDistance(area, p) <= 0f;
    }

    List<GameObject> Occupants()
    {
        var b = area.bounds;
        var list = AbilityKit.Enemies(b.center, b.extents.magnitude + standHeight, null);
        list.RemoveAll(e => !Contains(e.transform.position));
        return list;
    }

    void Update()
    {
        if (area == null) return;
        bool due = Time.time >= nextTick;
        bool shock = Electrified && Time.time >= nextShock;
        if (!due && !shock && !Iced && !Muddy && sliding.Count == 0 && mired.Count == 0) return;

        var inside = Occupants();

        // Ice: everyone slides. Mud: everyone but whoever made it is slowed and can't dash.
        Sync(sliding, Iced ? inside : null, m => m.SetTraction(key, iceTraction), m => m.ClearTraction(key));
        var stuck = Muddy ? inside.FindAll(e => e != mudBy) : null;
        Sync(mired, stuck, m => { m.SetDashBlocked(key, true); m.SetSpeedModifier(key, mudSlow); },
                           m => { m.SetDashBlocked(key, false); m.ClearSpeedModifier(key); });

        if (shock)
        {
            nextShock = Time.time + ShockTick;
            Color volt = Elements.ColorOf(Element.Lightning);
            foreach (var e in inside)
            {
                if (e == electrifiedBy) continue;
                DamageEvents.Deal(e, electrifyDps * ShockTick, electrifiedBy);
                StatusEffects.Of(e).StunAtLeast(0.4f, 0.3f);
                Vector3 chest = AbilityKit.Chest(e);
                AbilityKit.Zap(chest + Vector3.down, chest, volt, 0.12f, 0.14f);
            }
        }
        if (Electrified && Time.time >= nextCrackle)
        {
            nextCrackle = Time.time + 0.08f;
            foreach (var e in inside)
            {
                Vector3 feet = e.transform.position + Vector3.up * 0.1f;
                AbilityKit.Zap(feet, feet + Random.insideUnitSphere * 1.2f, Elements.ColorOf(Element.Lightning), 0.08f, 0.08f);
            }
        }

        if (!due) return;
        nextTick = Time.time + Tick;
        if (Iced || Muddy || !appliesStatus) return;   // frozen or churned water doesn't soak
        foreach (var e in inside)
            ElementReactions.ZoneHit(e, null, element, 0f);
    }

    void Sync(HashSet<PlayerMovement3D> set, List<GameObject> who, System.Action<PlayerMovement3D> on, System.Action<PlayerMovement3D> off)
    {
        var now = new HashSet<PlayerMovement3D>();
        if (who != null)
            foreach (var e in who)
            {
                var m = e.GetComponent<PlayerMovement3D>();
                if (m == null) continue;
                on(m);
                now.Add(m);
            }
        foreach (var m in set)
            if (m != null && !now.Contains(m)) off(m);
        set.Clear();
        set.UnionWith(now);
    }

    // ---------- Reactions ----------

    public void Electrify(GameObject by, float duration, float damagePerSecond)
    {
        if (element != Element.Water || Iced || Muddy) return;
        electrifiedBy = by;
        electrifyDps = damagePerSecond;
        electrifiedUntil = Mathf.Max(electrifiedUntil, Time.time + duration);
        nextShock = Time.time;
    }

    public void FreezeOver(GameObject by, float duration, float traction)
    {
        if (element != Element.Water) return;
        iceUntil = Time.time + duration;
        iceTraction = traction;
        electrifiedUntil = 0f;
    }

    public void MudOver(GameObject by, float duration, float slow)
    {
        if (element != Element.Water) return;
        mudUntil = Time.time + duration;
        mudSlow = slow;
        mudBy = by;
        electrifiedUntil = 0f;
    }

    // ---------- IElementZone ----------
    // Frozen water counts as frost, mud as earth, so they don't react as water meanwhile
    public Element ZoneElement => Iced ? Element.Frost : Muddy ? Element.Earth : element;
    public GameObject ZoneOwner => null;
    public Vector3 ZoneCenter => area != null ? area.bounds.center : transform.position;
    public float ZoneRadius => area != null ? Mathf.Max(area.bounds.extents.x, area.bounds.extents.z) : 1f;
    public float DistanceTo(Vector3 p) => area != null ? ElementZones.FlatDistance(area, p) : float.MaxValue;
    public void Consume() { }   // map terrain is permanent

    void OnDisable()
    {
        ElementZones.Unregister(this);
        foreach (var m in sliding) if (m != null) m.ClearTraction(key);
        foreach (var m in mired) if (m != null) { m.SetDashBlocked(key, false); m.ClearSpeedModifier(key); }
        sliding.Clear();
        mired.Clear();
    }
}
