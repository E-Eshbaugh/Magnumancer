using UnityEngine;

/// Singularity Launcher: lobs a black-hole grenade. Where it lands a singularity opens,
/// drags everyone nearby into its middle, then pops and blasts them back out.
public class SingularityLauncher : WonderWeapon
{
    public static float Range = 11f;

    public override int MaxAmmo => 3;
    protected override float Cooldown => 1f;

    protected override void Fire()
    {
        Singularity.Launch(gameObject, Muzzle, AbilityKit.AimPoint(gameObject, Range), color);
        Sfx.Play(SfxId.SingularityShot, transform.position);
        PowerFx.Flash(Muzzle, color, 5f, 4f, 0.2f);
    }
}
