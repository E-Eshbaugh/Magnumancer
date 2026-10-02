using UnityEngine;

/// Thunder Maul: the Smash hammer. A short-range slam in front of you that launches
/// everyone it catches across the map, crackling with lightning (Charged, Conduct on the
/// Soaked, and heavy enough to Shatter the frozen).
public class ThunderMaul : WonderWeapon
{
    public static float Reach = 3.6f;
    public static float Arc = 150f;
    public static float Damage = 28f;
    public static float Launch = 85f;
    public static float Lift = 6f;

    public override int MaxAmmo => 5;
    protected override float Cooldown => 0.75f;

    protected override void Fire()
    {
        Vector3 aim = Aim, from = transform.position;
        Vector3 slam = AbilityKit.Ground(from + aim * 1.8f + Vector3.up);
        Sfx.Play(SfxId.ThunderMaul, slam);
        Sfx.Play(SfxId.LandHeavy, slam, 1f, 0.7f);   // the hammer hitting the floor
        Sfx.Play(SfxId.Zap, slam);

        foreach (var e in InCone(from, aim, Reach, Arc))
        {
            Vector3 away = e.transform.position - from; away.y = 0f;
            Vector3 push = (aim * 0.6f + away.normalized * 0.4f).normalized;
            DamageEvents.Deal(e, Damage, gameObject);
            ElementReactions.AbilityHit(e, gameObject, Element.Lightning, Damage, heavy: true);
            AbilityKit.Knockback(e, push * Launch + Vector3.up * Lift);
            AbilityKit.Zap(Muzzle, AbilityKit.Chest(e), color, 0.25f, 0.3f);
            PowerFx.Sparks(AbilityKit.Chest(e), color, 16, 6f, 0.4f, 0.07f, 0.5f);
            Rumble.Play(e, 1f, 0.8f, 0.35f);
        }

        if (movement != null) movement.ApplyKnockback(aim * 9f);   // lunge into the swing
        AbilityKit.Shockwave(slam, Reach, color, 0.35f);
        AbilityKit.Shockwave(slam, Reach * 0.5f, Color.white, 0.25f);
        for (int i = 0; i < 4; i++)
        {
            Vector3 tip = slam + Quaternion.Euler(0f, Random.Range(-Arc * 0.5f, Arc * 0.5f), 0f) * aim * Random.Range(1.5f, Reach);
            AbilityKit.Zap(slam + Vector3.up * 0.2f, tip + Vector3.up * 0.2f, color, 0.2f, 0.2f);
        }
        PowerFx.Flash(slam + Vector3.up, color, 12f, 8f, 0.3f);
        PowerFx.Sparks(slam + Vector3.up * 0.2f, color, 35, 8f, 0.5f, 0.08f, 1.2f, Vector3.up, 160f);
        CameraShake.Shake(0.3f, 0.2f);
        Kick(1.3f);
    }
}
