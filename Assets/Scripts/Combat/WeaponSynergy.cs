using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Wizard ↔ weapon affinity (RuneBook.Affinity). While the equipped gun is one of the
/// wizard's favoured classes, their bonus is on. Swapping to a favoured gun flashes a
/// ring at the wizard's feet so you know it kicked in.
/// </summary>
[RequireComponent(typeof(PlayerMovement3D))]
public class WeaponSynergy : MonoBehaviour, IOutgoingDamageModifier
{
    WizardData wizard;
    RuneBook.Affinity affinity;
    PlayerMovement3D movement;
    AmmoControl ammo;
    LaserScope scope;
    WeaponData lastGun;
    bool active;
    float healPending;
    float hollowBonus;

    const string Key = "synergy";

    public bool Active => active;
    public RuneBook.Affinity Affinity => affinity;

    // ----- tuning (matches the RuneBook descriptions) -----
    const float SiegeDamage = 1.2f;       // Granite: standing still
    const float GroveSooner = 2f;         // Verdant: Resurgence starts this much sooner
    const float GroveRegen = 3f;          // ...or, without it, this much HP/s
    const float GroveDelay = 4f;          // ...after this long untouched
    const float WaveReload = 1.4f;        // Tidebound
    const float ForgeFireRate = 1.15f;    // Emberguard
    const float StormDash = 0.65f;        // Voltborn
    const float DeadeyeFocus = 2f;        // Frostwarden
    const float DeadeyeDamage = 1.15f;
    const float HollowPerClass = 0.04f;   // Hollow

    public static WeaponSynergy AddTo(GameObject player, WizardData wiz)
    {
        if (player == null || wiz == null) return null;
        var s = player.GetComponent<WeaponSynergy>();
        if (s == null) s = player.AddComponent<WeaponSynergy>();
        s.wizard = wiz;
        s.affinity = RuneBook.AffinityOf(wiz);
        s.CountLoadoutClasses();
        return s;
    }

    void Awake()
    {
        movement = GetComponent<PlayerMovement3D>();
        ammo = GetComponentInChildren<AmmoControl>(true);
        scope = GetComponentInChildren<LaserScope>(true);
    }

    void OnEnable() => DamageEvents.Damaged += OnDamaged;
    void OnDisable() => DamageEvents.Damaged -= OnDamaged;

    // The Hollow: +4% per distinct weapon class carried
    void CountLoadoutClasses()
    {
        hollowBonus = 0f;
        if (wizard == null || wizard.passive != PassiveType.LastRites || ammo == null || ammo.guns == null) return;
        var classes = new HashSet<WeaponClass>();
        foreach (var g in ammo.guns)
            if (g != null && g.weaponClass != WeaponClass.None) classes.Add(g.weaponClass);
        hollowBonus = HollowPerClass * classes.Count;
    }

    void Update()
    {
        if (affinity == null || ammo == null) return;
        var gun = ammo.currentGun;
        bool now = RuneBook.Favors(wizard, gun);

        if (gun != lastGun)
        {
            if (now && lastGun != null && affinity.classes.Length > 0) Announce();
            lastGun = gun;
        }
        if (now == active) return;
        active = now;
        Apply(active);
    }

    // Stat bonuses that live on other components
    void Apply(bool on)
    {
        switch (wizard.passive)
        {
            case PassiveType.BrandOfFlereous:
                ammo.SetFireRateModifier(Key, on ? ForgeFireRate : 1f);
                break;
            case PassiveType.Undercurrent:
                ammo.SetReloadSpeedModifier(Key, on ? WaveReload : 1f);
                break;
            case PassiveType.LightningReflex:
                movement.SetDashCooldownModifier(Key, on ? StormDash : 1f);
                break;
            case PassiveType.FractalshotShield:
                if (scope != null) scope.focusSpeedMultiplier = on ? DeadeyeFocus : 1f;
                break;
            case PassiveType.VerdantResurgence:
                var resurgence = GetComponent<VerdantResurgencePassive>();
                if (resurgence != null) resurgence.delayReduction = on ? GroveSooner : 0f;
                break;
        }
    }

    // Damage bonuses
    public float ModifyOutgoing(float amount)
    {
        if (wizard == null) return amount;
        switch (wizard.passive)
        {
            case PassiveType.LastRites:
                return amount * (1f + hollowBonus);
            case PassiveType.Stonebind:
                return active && movement.StickMagnitude < 0.1f ? amount * SiegeDamage : amount;
            case PassiveType.FractalshotShield:
                return active ? amount * DeadeyeDamage : amount;
        }
        return amount;
    }

    // Verdant without Verdant Resurgence (Thornhide rune): Grove Guard brings its own regen
    void OnDamaged(GameObject victim, GameObject attacker, float amount) { }

    void LateUpdate()
    {
        if (!active || wizard == null || wizard.passive != PassiveType.VerdantResurgence) return;
        if (GetComponent<VerdantResurgencePassive>() != null) return;
        var health = GetComponent<PlayerHealthControl>();
        if (health == null || health.IsDead || health.currentHealth >= health.maxHealth) return;
        if (Time.time - health.LastDamageTime < GroveDelay) { healPending = 0f; return; }
        healPending += GroveRegen * Time.deltaTime;
        int whole = Mathf.FloorToInt(healPending);
        if (whole > 0) { health.Heal(whole); healPending -= whole; }
    }

    void Announce()
    {
        AbilityKit.Shockwave(transform.position, 1.6f, GlowLine.Brighten(WizardSpawnEffect.ThemeColorOf(wizard)), 0.3f);
        Rumble.Play(movement.gamepad, 0.1f, 0.35f, 0.12f);
    }

    /// Blightward's Toxic Payload: grenades fired from a favoured gun leave poison behind
    public static void OnGrenadeExploded(GameObject owner, Vector3 at)
    {
        if (owner == null) return;
        var s = owner.GetComponent<WeaponSynergy>();
        if (s == null || !s.active || s.wizard == null || s.wizard.passive != PassiveType.VirulentShroud) return;
        var puddle = GroundHazard.Spawn(owner, AbilityKit.Ground(at + Vector3.up), 2.5f, 3f, AbilityKit.Theme(owner));
        puddle.damagePerSecond = 6f;
    }
}
