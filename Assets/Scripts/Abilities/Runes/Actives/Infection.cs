using UnityEngine;

/// Contagion's status: burns for a while and spreads to nearby enemies of the source.
public class Infection : MonoBehaviour
{
    public float duration = 6f;
    public float damagePerSecond = 5f;
    public float spreadRadius = 3f;

    GameObject source;
    Color color;
    float until, nextTick, nextSpread;
    LineRenderer ring;

    public static void Apply(GameObject target, GameObject source, Color color)
    {
        if (target == null) return;
        var inf = target.GetComponent<Infection>();
        if (inf == null) inf = target.AddComponent<Infection>();
        inf.source = source;
        inf.color = color;
        inf.until = Time.time + inf.duration;
    }

    void Start()
    {
        ring = GlowLine.Make(transform, "InfectionRing", 24, 0.08f, AbilityKit.Glow());
        ring.loop = true;
        nextSpread = Time.time + 1f;
    }

    void Update()
    {
        if (Time.time >= until || source == null) { Destroy(ring.gameObject); Destroy(this); return; }

        AbilityKit.Circle(ring, transform.position + Vector3.up * 0.1f, 0.6f + 0.1f * Mathf.Sin(Time.time * 8f));
        GlowLine.SetColor(ring, color, 0.8f);

        if (Time.time >= nextTick)
        {
            nextTick = Time.time + 0.5f;
            DamageEvents.Deal(gameObject, damagePerSecond * 0.5f, source);
            ElementReactions.AbilityHit(gameObject, source, Element.Poison, damagePerSecond * 0.5f);
        }
        if (Time.time >= nextSpread)
        {
            nextSpread = Time.time + 1f;
            foreach (var e in AbilityKit.Enemies(transform.position, spreadRadius, source))
                if (e != gameObject && e.GetComponent<Infection>() == null)
                {
                    Apply(e, source, color);
                    AbilityKit.Zap(AbilityKit.Chest(gameObject), AbilityKit.Chest(e), color, 0.15f, 0.12f);
                }
        }
    }
}
