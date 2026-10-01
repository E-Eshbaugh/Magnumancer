using System.Collections;
using UnityEngine;

/// Frost Cannon: a freezing blast down a wide cone that encases everyone in it in ice:
/// frozen solid, ready to Shatter.
public class FrostCannon : WonderWeapon
{
    public static float Range = 9f;
    public static float Cone = 60f;
    public static float Damage = 15f;
    public static float FreezeTime = 2.2f;
    public static float BlastSpeed = 30f;

    public override int MaxAmmo => 4;
    protected override float Cooldown => 1.1f;

    static readonly Color Mist = new Color(0.82f, 0.94f, 1f);

    protected override void Fire()
    {
        Vector3 aim = Aim, from = transform.position;
        ConeBlast.Spawn(Muzzle, aim, Range, Cone, BlastSpeed, Mist, Color.white, 90);
        PowerFx.Flash(Muzzle, color, 8f, 6f, 0.3f);
        PowerFx.IceShards(Muzzle + aim * 0.5f, color, 10, 6f, 0.1f);
        CameraShake.Shake(0.15f, 0.2f);
        if (movement != null) movement.ApplyKnockback(-aim * 7f);   // the cannon kicks

        foreach (var e in InCone(from, aim, Range, Cone))
            Run(Freeze(e, Flat(e.transform.position - from) / BlastSpeed));
        // water ahead freezes over
        ElementReactions.OnElementArea(from + aim * Range * 0.5f, Range * 0.5f, gameObject, Element.Frost);
    }

    IEnumerator Freeze(GameObject enemy, float delay)
    {
        GameObject caster = gameObject;
        Color ice = color;
        yield return new WaitForSeconds(delay);
        if (enemy == null || !enemy.activeInHierarchy) yield break;

        DamageEvents.Deal(enemy, Damage, caster);
        ElementReactions.AbilityHit(enemy, caster, Element.Frost, Damage);
        var fx = StatusEffects.Of(enemy);
        fx.ClearFreeze();
        fx.Root(FreezeTime);
        fx.MarkFrozen(FreezeTime);   // frozen solid: Shatter-able
        new GameObject("IceEncase").AddComponent<IceEncase>().Init(enemy, FreezeTime, 5, ice, null);
    }
}
