using System.Collections;
using UnityEngine;

/// <summary>
/// A throwable in flight and what it does when it lands:
///  • Hex Grenade: bursts in a random element, leaving that element's status on everyone
///    caught (or setting off a reaction with what they already carry).
///  • Goblin Bomb: sticks to the first enemy it touches (or the floor), fizzes, then blows
///    up big. Whoever wears it feels it ticking.
///  • Portal Stone: opens a portal at your feet and one where it lands, linked both ways
///    for a few seconds. Anyone can use them.
/// Lives on its own object so it finishes even if the thrower is knocked out mid-throw.
/// </summary>
public class ThrownItem : MonoBehaviour
{
    public static float ThrowRange = 9f;

    public static float HexRadius = 3.2f;
    public static float HexDamage = 22f;

    public static float BombRadius = 3.8f;
    public static float BombDamage = 50f;
    public static float BombFuse = 1.5f;
    public static float BombStickRadius = 1.1f;

    public static float PortalTime = 5f;
    public static float PortalRadius = 0.9f;

    ItemBook.Def def;
    GameObject thrower;
    Transform model;

    public static void Launch(ItemBook.Def def, GameObject thrower)
    {
        var go = new GameObject($"Thrown {def.name}");
        var t = go.AddComponent<ThrownItem>();
        t.def = def;
        t.thrower = thrower;
        t.StartCoroutine(t.Fly());
    }

    IEnumerator Fly()
    {
        Vector3 aim = AbilityKit.AimDir(thrower);
        Vector3 from = AbilityKit.Chest(thrower) + aim * 0.6f;
        Vector3 to = AbilityKit.AimPoint(thrower, ThrowRange);
        transform.position = from;
        Sfx.Play(SfxId.Throw, from);
        model = ItemVisuals.Model(def.id, transform);
        model.localScale = Vector3.one * 0.8f;

        VoidRift rift = null;
        if (def.id == ItemId.PortalStone)
        {
            rift = new GameObject("PortalStone").AddComponent<VoidRift>();
            rift.Begin(thrower, AbilityKit.Ground(thrower.transform.position + Vector3.up), PortalRadius, def.color, 1f, 0f);
        }

        float dist = Vector3.Distance(from, to);
        float flight = Mathf.Lerp(0.35f, 0.7f, Mathf.Clamp01(dist / ThrowRange));
        float height = Mathf.Lerp(1f, 2.5f, Mathf.Clamp01(dist / ThrowRange));
        float t = 0f, nextSpark = 0f;
        while (t < flight)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / flight);
            transform.position = Vector3.Lerp(from, to, k) + Vector3.up * (4f * height * k * (1f - k));
            model.Rotate(Vector3.up, 540f * Time.deltaTime, Space.World);
            if (Time.time >= nextSpark)
            {
                nextSpark = Time.time + 0.05f;
                PowerFx.Sparks(transform.position, def.color, 2, 1f, 0.3f, 0.05f, 0f);
            }

            // the goblin bomb grabs the first enemy it passes
            if (def.id == ItemId.StickyBomb)
            {
                var victim = Closest(transform.position, BombStickRadius);
                if (victim != null) { yield return Stick(victim, transform.position); yield break; }
            }
            yield return null;
        }
        transform.position = to;

        switch (def.id)
        {
            case ItemId.HexGrenade:
                HexBurst(to);
                Destroy(gameObject);
                break;
            case ItemId.StickyBomb:
            {
                yield return Stick(Closest(to, BombStickRadius * 1.5f), to);
                break;
            }
            case ItemId.PortalStone:
                if (rift != null) rift.Link(to, PortalTime);
                Sfx.Play(SfxId.Blink, to);
                Sfx.Play(SfxId.CastVoid, to, 0.6f, 1.2f);
                AbilityKit.Shockwave(to, 1.8f, def.color, 0.35f);
                Destroy(gameObject);
                break;
            default:
                Destroy(gameObject);
                break;
        }
    }

    // ---------- Hex Grenade ----------

    void HexBurst(Vector3 at)
    {
        var element = (Element)Random.Range(1, 9);   // any element but None
        Color c = Elements.ColorOf(element);
        Sfx.Play(PlayerSfx.CastSound(element), at);   // it sounds like the element it rolled

        foreach (var e in AbilityKit.Enemies(at, HexRadius, thrower))
        {
            float k = 1f - Mathf.Clamp01(Vector3.Distance(at, e.transform.position) / HexRadius);
            float dmg = HexDamage * Mathf.Lerp(0.5f, 1f, k);
            DamageEvents.Deal(e, dmg, thrower);
            if (ElementReactions.AbilityHit(e, thrower, element, dmg)) continue;
            // the statuses abilities apply themselves
            var fx = StatusEffects.Of(e);
            switch (element)
            {
                case Element.Frost: fx.FreezeAtLeast(3); break;
                case Element.Nature: fx.Root(0.8f); break;
                case Element.Void: if (thrower != null) fx.MarkVoid(thrower); break;
            }
        }
        ElementReactions.OnElementArea(at, HexRadius, thrower, element);
        Explosions.AffectWorld(at, HexRadius, HexDamage, gameObject);

        var flavor = Elements.FlavorOf(element);
        for (int i = 0; i < 16; i++)
            BulletFX.Mote(flavor, c, at + Vector3.up * 0.5f + Random.insideUnitSphere * HexRadius * 0.6f, 1.6f);
        AbilityKit.Shockwave(at, HexRadius, c, 0.4f);
        AbilityKit.Shockwave(at, HexRadius * 0.55f, def.color, 0.3f);
        PowerFx.Flash(at + Vector3.up, c, 9f, 7f, 0.35f);
        PowerFx.Sparks(at + Vector3.up * 0.3f, c, 30, 7f, 0.6f, 0.08f, 1f, Vector3.up, 150f);
        ReactionPopup.Show($"{element.ToString().ToUpperInvariant()} HEX!", def.color, c, at + Vector3.up * 2.4f, 0.8f);
        CameraShake.Shake(0.15f, 0.2f);
    }

    // ---------- Goblin Bomb ----------

    IEnumerator Stick(GameObject victim, Vector3 at)
    {
        Vector3 offset = Vector3.up * 1.2f;
        stuckTo = victim;
        bool onVictim = victim != null;
        if (onVictim)
        {
            ReactionPopup.Show("STUCK!", def.color, Color.white, AbilityKit.Chest(victim) + Vector3.up * 1.6f, 0.7f);
            Rumble.Play(victim, 0.3f, 0.5f, 0.15f);
            DamageEvents.Killed += Unstick;
        }
        else at = AbilityKit.Ground(at + Vector3.up) + Vector3.up * 0.3f;
        Sfx.Play(SfxId.StickyThunk, at);
        Sfx.Play(SfxId.FuseHiss, at);

        var light = gameObject.AddComponent<Light>();
        light.type = LightType.Point; light.color = def.color; light.range = 3f; light.shadows = LightShadows.None;

        float t = 0f;
        bool wasOn = false;
        while (t < BombFuse)
        {
            t += Time.deltaTime;
            if (onVictim && (stuckTo == null || !stuckTo.activeInHierarchy)) onVictim = false;
            if (onVictim) at = stuckTo.transform.position + offset;
            transform.position = at;

            // fizzes faster as it's about to go
            float rate = Mathf.Lerp(4f, 16f, t / BombFuse);
            bool on = Mathf.Repeat(t * rate, 1f) < 0.5f;
            light.intensity = on ? 4f : 0.5f;
            model.localScale = Vector3.one * (on ? 0.9f : 0.75f);
            if (on && onVictim) Rumble.Play(stuckTo, 0.1f, 0.3f, 0.05f, fade: false);
            if (on && !wasOn) Sfx.Play(SfxId.UiMove, at, 1f, Mathf.Lerp(1.3f, 1.9f, t / BombFuse));   // beep... beep.. beepbeep
            wasOn = on;
            if (Random.value < 0.3f) PowerFx.Sparks(at + Vector3.up * 0.2f, Color.Lerp(def.color, Color.white, 0.5f), 1, 2f, 0.25f, 0.05f, 0.5f);
            yield return null;
        }
        DamageEvents.Killed -= Unstick;
        Explode(at);
        Destroy(gameObject);
    }

    GameObject stuckTo;

    // the wearer lost a life (and respawns elsewhere): the bomb drops where they fell
    void Unstick(GameObject victim, GameObject attacker, Vector3 at)
    {
        if (victim == stuckTo) stuckTo = null;
    }

    void Explode(Vector3 at)
    {
        foreach (var e in AbilityKit.Enemies(at, BombRadius, thrower))
        {
            float k = 1f - Mathf.Clamp01(Vector3.Distance(at, e.transform.position) / BombRadius);
            float dmg = BombDamage * Mathf.Lerp(0.4f, 1f, k);
            DamageEvents.Deal(e, dmg, thrower);
            ElementReactions.AbilityHit(e, thrower, Element.None, dmg, heavy: true);
        }
        Explosions.AffectWorld(at, BombRadius, BombDamage, gameObject);

        Vector3 ground = AbilityKit.Ground(at + Vector3.up);
        AbilityKit.Shockwave(ground, BombRadius, def.color, 0.45f);
        AbilityKit.Shockwave(ground, BombRadius * 0.6f, Color.white, 0.3f);
        PowerFx.Flash(at, def.color, 12f, 9f, 0.4f);
        PowerFx.Sparks(at, def.color, 45, 9f, 0.7f, 0.1f, 1.2f, Vector3.up, 160f);
        PowerFx.Puffs(at, new Color(0.15f, 0.2f, 0.08f, 1f), 12, 3f, 1.2f, 1.4f, lift: 1.5f);
        CameraShake.Shake(0.3f, 0.25f);
    }

    // nearest enemy of the thrower within radius of p
    GameObject Closest(Vector3 p, float radius)
    {
        GameObject best = null;
        float bestDist = float.MaxValue;
        foreach (var e in AbilityKit.Enemies(p, radius, thrower))
        {
            float d = (AbilityKit.Chest(e) - p).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    void OnDestroy() => DamageEvents.Killed -= Unstick;
}
