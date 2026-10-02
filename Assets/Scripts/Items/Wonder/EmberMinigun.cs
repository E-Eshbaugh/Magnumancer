using UnityEngine;

/// <summary>
/// Ember Minigun: hold the trigger to spin it up, then it pours out fire rounds that
/// brand everyone they hit. Heavy: you move slower while you hold it. When the last
/// round is gone it melts down, leaving a pool of lava at your feet (it can't hurt you).
/// </summary>
public class EmberMinigun : WonderWeapon
{
    public static int Rounds = 150;
    public static float RoundsPerSecond = 18f;
    public static int Damage = 6;
    public static float Spread = 7f;
    public static float SpinUpTime = 0.5f;
    public static float CarrySpeed = 0.75f;

    public override int MaxAmmo => Rounds;
    protected override bool Automatic => true;

    const string SpeedKey = "ember-minigun";

    float spin, shownHeat = -1f;
    Transform barrels;
    GameObject bullet;
    MeshRenderer[] parts;

    protected override void OnEquip()
    {
        foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name == "Barrels") { barrels = t; break; }
        parts = model.GetComponentsInChildren<MeshRenderer>();
        bullet = BulletPrefab();
        if (movement != null) movement.SetSpeedModifier(SpeedKey, CarrySpeed);
    }

    // A bullet prefab to shoot: the player's fire round, else whatever their guns use
    GameObject BulletPrefab()
    {
        if (ammo == null) return null;
        if (IsBullet(ammo.fireBulletPrefab)) return ammo.fireBulletPrefab;
        if (ammo.wizard != null && IsBullet(ammo.wizard.customBulletPrefab)) return ammo.wizard.customBulletPrefab;
        if (ammo.guns != null)
            foreach (var g in ammo.guns)
            {
                if (g == null) continue;
                if (IsBullet(g.baseAmmoType)) return g.baseAmmoType;
                if (IsBullet(g.ammoType)) return g.ammoType;
            }
        return null;
    }

    static bool IsBullet(GameObject prefab) => prefab != null && prefab.GetComponent<Bullet>() != null;

    protected override void Tick(bool trigger)
    {
        spin = Mathf.MoveTowards(spin, trigger ? 1f : 0f, Time.deltaTime / (trigger ? SpinUpTime : 0.8f));
        if (barrels != null) barrels.Rotate(Vector3.forward, spin * 1500f * Time.deltaTime, Space.Self);

        var pad = Pad;
        if (spin > 0.05f) Rumble.Hold(pad, SpeedKey, 0.1f * spin, 0.25f * spin);
        else Rumble.Release(pad, SpeedKey);

        // the barrels whine up as they spin
        if (spin > 0.02f) { Sfx.StartLoop(this, SfxId.EmberSpinLoop); Sfx.SetLoop(this, spin, 0.5f + 0.7f * spin); }
        else Sfx.StopLoop(this);

        // hotter as it empties
        float heat = 1f - Ammo / (float)MaxAmmo;
        if (Mathf.Abs(heat - shownHeat) > 0.02f)
        {
            shownHeat = heat;
            foreach (var r in parts)
                if (r != null) ItemVisuals.Tint(r, Color.Lerp(color, Color.white, heat * 0.6f), 2f + heat * 2f);
        }

        if (!trigger || spin < 1f || Time.time < nextFire || Ammo <= 0) return;
        nextFire = Time.time + 1f / RoundsPerSecond;
        Ammo--;
        if (fire != null && bullet != null)
        {
            fire.Shoot(bullet, Spread, 0.12f, Damage, element: Element.Fire);
            Sfx.Play(SfxId.EmberShot, transform.position, 1f, Mathf.Lerp(1f, 1.15f, 1f - Ammo / (float)MaxAmmo));
            if (fire.firePoint != null)
                BulletFX.MuzzleFlash(fire.Owner, fire.firePoint.position, fire.ShotDirection(), BulletFX.ShotPower(Damage));
        }
        Kick(0.25f, rumble: false);
        if (Ammo <= 0) Meltdown();
    }

    protected override void Fire() { }   // fires from Tick

    void Meltdown()
    {
        Vector3 ground = AbilityKit.Ground(transform.position + Vector3.up);
        var lava = GroundHazard.Spawn(gameObject, ground, 2.4f, 3.5f, color);
        lava.element = Element.Fire;
        lava.damagePerSecond = 12f;
        lava.slowMultiplier = 0.7f;
        EffectPool.Spawn(ground, 2.4f, 3.5f, EffectPool.Style.Lava);
        Sfx.StopLoop(this);
        Sfx.Play(SfxId.ExplosionBig, ground, 0.8f, 1.2f);
        Sfx.Play(SfxId.CastFire, ground);
        PowerFx.Flash(ground + Vector3.up, color, 10f, 7f, 0.4f);
        PowerFx.Sparks(ground + Vector3.up * 0.5f, color, 40, 7f, 0.7f, 0.08f, 1f, Vector3.up, 150f);
        PowerFx.Puffs(ground + Vector3.up, new Color(0.15f, 0.1f, 0.08f, 1f), 8, 2f, 1f, 1.2f, lift: 1.5f);
        ReactionPopup.Show("MELTDOWN!", color, Color.white, AbilityKit.Chest(gameObject) + Vector3.up * 1.8f, 0.8f);
        Invoke(nameof(OutOfAmmo), 0.3f);
    }

    protected override void OnEnd()
    {
        if (movement != null) movement.ClearSpeedModifier(SpeedKey);
        Rumble.Release(Pad, SpeedKey);
        Sfx.StopLoop(this);
    }
}
