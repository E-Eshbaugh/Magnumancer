using UnityEngine;

/// <summary>
/// Small, readable hints of the element statuses on a target, so players can see a
/// reaction coming without any HUD: Soaked drips, Charged crackles, Poisoned oozes,
/// Burning sheds embers, Staggered kicks up dust. Motes go through BulletFX's shared
/// particle systems. Added by StatusEffects when a status lands; sleeps when clear.
/// </summary>
public class ElementStatusFx : MonoBehaviour
{
    const float Interval = 0.11f;

    StatusEffects fx;
    float next;
    int turn;

    public static void On(GameObject target)
    {
        if (target == null) return;
        var s = target.GetComponent<ElementStatusFx>();
        if (s == null) s = target.AddComponent<ElementStatusFx>();
        s.enabled = true;
    }

    void Update()
    {
        if (fx == null && (fx = GetComponent<StatusEffects>()) == null) { enabled = false; return; }

        bool soaked = fx.IsSoaked, charged = fx.IsCharged, poisoned = fx.IsPoisoned;
        bool burning = fx.IsBurning, staggered = fx.IsStaggered;
        if (!soaked && !charged && !poisoned && !burning && !staggered) { enabled = false; return; }

        if (Time.time < next) return;
        next = Time.time + Interval;
        turn++;

        Vector3 body = transform.position + Vector3.up * Random.Range(0.4f, 1.7f)
                       + new Vector3(Random.Range(-0.35f, 0.35f), 0f, Random.Range(-0.35f, 0.35f));

        if (soaked)
            BulletFX.Mote(BulletFX.Flavor.Water, Elements.ColorOf(Element.Water), body, 0.8f);
        if (charged)
        {
            Color volt = Elements.ColorOf(Element.Lightning);
            BulletFX.Mote(BulletFX.Flavor.Volt, volt, body, 0.8f);
            if (turn % 3 == 0)
                AbilityKit.Zap(body, body + Random.onUnitSphere * 0.5f, volt, 0.06f, 0.05f);
        }
        if (poisoned && turn % 2 == 0)
            BulletFX.Mote(BulletFX.Flavor.Toxic, Elements.ColorOf(Element.Poison), body, 0.9f);
        if (burning && turn % 2 == 1)
            BulletFX.Mote(BulletFX.Flavor.Embers, Elements.ColorOf(Element.Fire), body, 0.8f);
        if (staggered)
            BulletFX.Mote(BulletFX.Flavor.Grit, Elements.ColorOf(Element.Earth), transform.position + Vector3.up * 2f, 0.7f);
    }
}
