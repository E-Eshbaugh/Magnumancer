using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Tidebound — Rune III: a wave that damages and hurls back everything in front of you.
public class TidalSurgeAbility : MonoBehaviour, IActiveAbility
{
    public float range = 6f;
    public float coneDegrees = 75f;
    public float damage = 25f;
    public float knockback = 20f;

    public void Activate(GameObject caster)
    {
        Vector3 dir = AbilityKit.AimDir(caster);
        Vector3 origin = caster.transform.position;
        Color theme = AbilityKit.Theme(caster);

        var fx = WaveSurgeFx.Spawn(origin, dir, range, coneDegrees, theme);
        CameraShake.Shake(0.15f, 0.35f);

        // each enemy is hit when the first wave actually reaches them
        foreach (var e in AbilityKit.Enemies(origin, range, caster))
        {
            Vector3 to = e.transform.position - origin; to.y = 0f;
            if (Vector3.Angle(dir, to) > coneDegrees * 0.5f) continue;
            StartCoroutine(Hit(e, caster, to, fx.ArrivalTime(to.magnitude)));
        }
    }

    IEnumerator Hit(GameObject enemy, GameObject caster, Vector3 to, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (enemy == null) yield break;
        DamageEvents.Deal(enemy, damage, caster);
        AbilityKit.Knockback(enemy, (to.normalized + Vector3.up * 0.1f) * knockback);
        ElementReactions.AbilityHit(enemy, caster, damage);
        Rumble.Play(enemy, 0.6f, 0.4f, 0.25f);
        Sfx.Play(SfxId.Splash, enemy.transform.position);
    }
}
