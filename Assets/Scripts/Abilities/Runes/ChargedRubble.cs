using UnityEngine;

/// <summary>
/// Magnetize: a chunk of rubble crackling with lightning. It lands, then arcs at anyone
/// who comes close (never whoever charged it): damage and a Lightning hit, so it Charges
/// people and can set off Conduct on the Soaked. It's also a tiny lightning zone, so
/// rubble landing in water electrifies it.
/// </summary>
public class ChargedRubble : MonoBehaviour, IElementZone
{
    GameObject owner;
    float reach, damage, interval, until, nextArc, nextCrackle;
    Transform rock;
    bool landed;

    public static ChargedRubble Throw(GameObject owner, Vector3 from, Vector3 to, float reach, float damage, float interval, float life)
    {
        var go = new GameObject("ChargedRubble");
        go.transform.position = from;
        var r = go.AddComponent<ChargedRubble>();
        r.owner = owner; r.reach = reach; r.damage = damage; r.interval = interval;
        r.until = Time.time + life;
        r.Build();
        r.StartCoroutine(r.Fly(from, to));
        return r;
    }

    void Build()
    {
        var chunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
        chunk.name = "Rock";
        Destroy(chunk.GetComponent<Collider>());
        chunk.transform.SetParent(transform, false);
        chunk.transform.localScale = new Vector3(Random.Range(0.35f, 0.5f), Random.Range(0.25f, 0.4f), Random.Range(0.35f, 0.5f));
        chunk.transform.localRotation = Random.rotation;
        var mr = chunk.GetComponent<MeshRenderer>();
        mr.sharedMaterial = RockDebris.StoneMaterial(default);
        rock = chunk.transform;
    }

    System.Collections.IEnumerator Fly(Vector3 from, Vector3 to)
    {
        yield return AbilityKit.Lob(transform, from, to, 0.35f, 1.2f);
        landed = true;
        transform.position = to + Vector3.up * 0.15f;
        RockDebris.Dust(to, 0.5f, 3);
        ElementZones.Register(this);
    }

    void Update()
    {
        if (Time.time >= until) { Crumble(); return; }
        Color volt = Elements.ColorOf(Element.Lightning);
        Vector3 at = transform.position + Vector3.up * 0.2f;

        // crackling so everyone can see it's live
        if (Time.time >= nextCrackle)
        {
            nextCrackle = Time.time + Random.Range(0.05f, 0.15f);
            AbilityKit.Zap(at, at + Random.onUnitSphere * 0.5f, volt, 0.06f, 0.06f);
        }
        if (!landed || Time.time < nextArc) return;

        foreach (var e in AbilityKit.Enemies(transform.position, reach, owner))
        {
            nextArc = Time.time + interval;
            Vector3 chest = AbilityKit.Chest(e);
            AbilityKit.Zap(at, chest, volt, 0.15f, 0.16f);
            PowerFx.Sparks(chest, volt, 10, 5f, 0.3f, 0.05f, 0.5f);
            ElementReactions.DealAs(Reaction.Magnetize, e, damage, owner);
            ElementReactions.AbilityHit(e, owner, Element.Lightning, damage);
            break; // one arc per pulse
        }
    }

    void Crumble()
    {
        RockDebris.Burst(transform.position, 4, 2f, 0.15f);
        Destroy(gameObject);
    }

    // ---------- IElementZone ----------
    public Element ZoneElement => Element.Lightning;
    public GameObject ZoneOwner => owner;
    public Vector3 ZoneCenter => transform.position;
    public float ZoneRadius => 1f;
    public float DistanceTo(Vector3 p) => ElementZones.FlatDistance(transform.position, 1f, p);
    public void Consume() { }

    void OnDestroy() => ElementZones.Unregister(this);
}
