using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An area on the ground that carries an element (poison clouds, lava, whirlpools,
/// static fields...). Zones register with ElementZones so reactions can find them:
/// a fire bullet through a poison cloud Combusts it, Conduct electrifies water zones.
/// Register in Start (owners are set right after spawning) and unregister in OnDisable.
/// </summary>
public interface IElementZone
{
    Element ZoneElement { get; }
    GameObject ZoneOwner { get; }
    Vector3 ZoneCenter { get; }
    /// Rough reach from the center, for zone-vs-zone overlap
    float ZoneRadius { get; }
    /// Flat distance from p to the zone (0 = inside)
    float DistanceTo(Vector3 p);
    /// A reaction used the zone up (a Combusted cloud is gone)
    void Consume();
}

/// Water zones that Conduct can electrify
public interface IElectrifiable
{
    void Electrify(GameObject by, float duration, float damagePerSecond);
}

/// Water zones that Brittle freezes into slippery ice
public interface IFreezable
{
    void FreezeOver(GameObject by, float duration, float iceTraction);
}

/// Water zones that Mudslide churns into mud
public interface IMuddable
{
    void MudOver(GameObject by, float duration, float slow);
}

public static class ElementZones
{
    static readonly List<IElementZone> zones = new();

    /// Zones stop counting this far above/below (bullets over a cloud on a lower floor)
    const float MaxHeightGap = 3f;

    public static void Register(IElementZone zone)
    {
        if (zone == null || zones.Contains(zone)) return;
        zones.Add(zone);
        ElementReactions.OnZoneSpawned(zone);
    }

    public static void Unregister(IElementZone zone) => zones.Remove(zone);

    static bool Alive(IElementZone z) => z is Object o ? o != null : z != null;

    /// Zones of `element` that p is inside of (or within pad of)
    public static List<IElementZone> At(Vector3 p, Element element, float pad = 0f)
    {
        var found = new List<IElementZone>();
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            var z = zones[i];
            if (!Alive(z)) { zones.RemoveAt(i); continue; }
            if (z.ZoneElement != element) continue;
            if (Mathf.Abs(z.ZoneCenter.y - p.y) > MaxHeightGap) continue;
            if (z.DistanceTo(p) <= pad) found.Add(z);
        }
        return found;
    }

    /// Zones of `element` touching a circle (area bursts, other zones)
    public static List<IElementZone> Overlapping(Vector3 center, float radius, Element element, IElementZone except = null)
    {
        var found = new List<IElementZone>();
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            var z = zones[i];
            if (!Alive(z)) { zones.RemoveAt(i); continue; }
            if (z == except || z.ZoneElement != element) continue;
            if (Mathf.Abs(z.ZoneCenter.y - center.y) > MaxHeightGap) continue;
            if (z.DistanceTo(center) <= radius) found.Add(z);
        }
        return found;
    }

    /// Two zones touch (either reaches the other's center, or their circles overlap)
    public static bool Touch(IElementZone a, IElementZone b)
        => a.DistanceTo(b.ZoneCenter) <= b.ZoneRadius || b.DistanceTo(a.ZoneCenter) <= a.ZoneRadius;

    /// First zone of `element` along a segment (a bullet's step)
    public static IElementZone AlongSegment(Vector3 from, Vector3 to, Element element, float pad = 0.3f)
    {
        float len = Vector3.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.5f));
        for (int i = zones.Count - 1; i >= 0; i--)
        {
            var z = zones[i];
            if (!Alive(z)) { zones.RemoveAt(i); continue; }
            if (z.ZoneElement != element) continue;
            for (int s = 0; s <= steps; s++)
            {
                Vector3 p = Vector3.Lerp(from, to, s / (float)steps);
                if (Mathf.Abs(z.ZoneCenter.y - p.y) > MaxHeightGap) break;
                if (z.DistanceTo(p) <= pad) return z;
            }
        }
        return null;
    }

    /// Flat distance from p to a collider's volume (0 inside)
    public static float FlatDistance(Collider col, Vector3 p)
    {
        if (col == null) return float.MaxValue;
        Vector3 probe = new Vector3(p.x, col.bounds.center.y, p.z);
        // ClosestPoint doesn't support non-convex mesh colliders (map floor planes): use bounds
        Vector3 c = col is MeshCollider mesh && !mesh.convex ? col.bounds.ClosestPoint(probe) : col.ClosestPoint(probe);
        c.y = probe.y;
        return Vector3.Distance(c, probe);
    }

    public static float FlatDistance(Vector3 center, float radius, Vector3 p)
    {
        Vector3 d = p - center; d.y = 0f;
        return Mathf.Max(0f, d.magnitude - radius);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => zones.Clear();
}
