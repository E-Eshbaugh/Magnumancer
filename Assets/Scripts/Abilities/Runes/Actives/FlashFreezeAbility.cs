using System.Collections;
using UnityEngine;
using Magnumancer.Abilities;

/// Frostwarden — Rune III: Flash Freeze. A frost pulse cashes in every freeze counter
/// around you: each chilled enemy is encased in ice for longer (and hurt more) the more
/// counters they carried, then the ice shatters. If nobody was chilled, the pulse chills
/// and briefly freezes anyone close instead.
public class FlashFreezeAbility : MonoBehaviour, IActiveAbility
{
    public float range = 14f;
    public float freezePerStack = 0.35f;
    public float damagePerStack = 6f;
    [Tooltip("Fallback when nobody had counters: freeze everyone this close")]
    public float fallbackRadius = 6f;
    public float fallbackFreeze = 0.6f;
    public float pulseSpeed = 30f;

    static readonly Color Ice = new Color(0.6f, 0.88f, 1f);
    static readonly Color Mist = new Color(0.82f, 0.94f, 1f);

    public void Activate(GameObject caster)
    {
        Vector3 origin = caster.transform.position;
        Color theme = AbilityKit.Theme(caster);

        // the pulse
        RollingSmokeFx.Spawn(origin, range * 0.6f, 0.45f, Mist, Color.white);
        AbilityKit.Shockwave(origin, range, Ice, 0.5f);
        AbilityKit.Shockwave(origin, range * 0.5f, Color.white, 0.3f);
        PowerFx.Flash(origin + Vector3.up, Ice, 10f, 10f, 0.5f);
        PowerFx.Sparks(origin + Vector3.up, Mist, 40, 10f, 0.5f, 0.08f, 0.6f);
        CameraShake.Shake(0.2f, 0.25f);
        Rumble.Play(caster, 0.4f, 0.9f, 0.3f);

        var enemies = AbilityKit.Enemies(origin, range, caster);
        bool anyChilled = false;
        foreach (var e in enemies)
            if (StatusEffects.Of(e).FreezeStacks > 0) { anyChilled = true; break; }

        foreach (var e in enemies)
        {
            var fx = StatusEffects.Of(e);
            int stacks = fx.FreezeStacks;
            float d = Vector3.Distance(origin, e.transform.position);
            if (anyChilled && stacks <= 0) continue;
            if (!anyChilled && d > fallbackRadius) continue;
            StartCoroutine(Freeze(caster, e, stacks, d / pulseSpeed, theme));
        }
    }

    IEnumerator Freeze(GameObject caster, GameObject enemy, int stacks, float delay, Color theme)
    {
        yield return new WaitForSeconds(delay);
        if (enemy == null) yield break;
        var fx = StatusEffects.Of(enemy);

        float time = stacks > 0 ? freezePerStack * stacks : fallbackFreeze;
        if (stacks > 0) DamageEvents.Deal(enemy, damagePerStack * stacks, caster);
        fx.ClearFreeze();
        fx.Root(time);
        if (stacks <= 0) { fx.AddFreeze(); fx.AddFreeze(); } // the fallback pulse leaves them chilled

        // a frost beam finds them, then the ice closes in
        if (caster != null)
        {
            var beam = new GameObject("FrostBeam");
            var lr = GlowLine.Make(beam.transform, "Beam", 2, 0.18f, AbilityKit.Glow());
            lr.SetPosition(0, AbilityKit.Chest(caster));
            lr.SetPosition(1, AbilityKit.Chest(enemy));
            lr.startColor = Color.white; lr.endColor = Ice;
            Destroy(beam, 0.15f);
        }
        new GameObject("IceEncase").AddComponent<IceEncase>().Init(enemy, time, Mathf.Max(1, stacks), Ice, AbilityKit.Wizard(caster));
    }
}
