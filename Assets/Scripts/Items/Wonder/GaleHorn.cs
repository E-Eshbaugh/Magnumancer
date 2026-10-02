using System.Collections;
using UnityEngine;

/// Gale Horn: a huge gust down a long cone that shoves everyone in it far away. Little
/// damage, all push: deadly near ledges and hazards. Heavy enough to Shatter the frozen
/// and Stagger everyone else.
public class GaleHorn : WonderWeapon
{
    public static float Range = 12f;
    public static float Cone = 65f;
    public static float Damage = 4f;
    public static float Shove = 60f;
    public static float GustSpeed = 25f;

    public override int MaxAmmo => 4;
    protected override float Cooldown => 0.9f;

    static readonly Color Air = new Color(0.9f, 0.97f, 1f);

    protected override void Fire()
    {
        Vector3 aim = Aim, from = transform.position;
        Sfx.Play(SfxId.GaleHorn, from);
        Sfx.Play(SfxId.Gust, from);
        ConeBlast.Spawn(Muzzle, aim, Range, Cone, GustSpeed, Air, color, 80);
        for (int i = 0; i < 3; i++) Run(Ring(Muzzle, aim, i * 0.07f, color));
        PowerFx.Flash(Muzzle, color, 6f, 5f, 0.25f);
        CameraShake.Shake(0.15f, 0.25f);
        if (movement != null) movement.ApplyKnockback(-aim * 5f);

        foreach (var e in InCone(from, aim, Range, Cone))
            Run(Blow(e, aim, Flat(e.transform.position - from) / GustSpeed));
    }

    IEnumerator Blow(GameObject enemy, Vector3 aim, float delay)
    {
        GameObject caster = gameObject;
        yield return new WaitForSeconds(delay);
        if (enemy == null || !enemy.activeInHierarchy || caster == null) yield break;
        Vector3 away = enemy.transform.position - caster.transform.position; away.y = 0f;
        Vector3 push = (aim * 0.8f + away.normalized * 0.2f).normalized;
        AbilityKit.Knockback(enemy, push * Shove);
        DamageEvents.Deal(enemy, Damage, caster);
        ElementReactions.AbilityHit(enemy, caster, Element.None, Damage, heavy: true);
        Rumble.Play(enemy, 0.6f, 0.4f, 0.3f);
    }

    // Rings of air rolling out of the horn's bell
    static IEnumerator Ring(Vector3 origin, Vector3 aim, float delay, Color color)
    {
        yield return new WaitForSeconds(delay);
        var go = new GameObject("GaleRing");
        Object.Destroy(go, 1f);   // safety net
        var lr = GlowLine.Make(go.transform, "Ring", 28, 0.12f, AbilityKit.Glow());
        lr.loop = true;
        Vector3 side = Vector3.Cross(Vector3.up, aim).normalized;
        float t = 0f, time = 0.45f;
        while (t < time)
        {
            t += Time.deltaTime;
            float k = t / time;
            Vector3 c = origin + aim * Range * 0.8f * k;
            float r = Mathf.Lerp(0.4f, Range * Mathf.Tan(Cone * 0.5f * Mathf.Deg2Rad) * 0.5f, k);
            for (int i = 0; i < 28; i++)
            {
                float a = i / 28f * Mathf.PI * 2f;
                lr.SetPosition(i, c + (side * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a) * 0.6f) * r);
            }
            GlowLine.SetColor(lr, color, 0.8f * (1f - k));
            yield return null;
        }
        Object.Destroy(go);
    }
}
