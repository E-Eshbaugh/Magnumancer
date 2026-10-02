using System.Reflection;
using UnityEngine;

/// <summary>
/// A player's own sounds: footsteps, jumps, landings, dashes, gun handling (draw, dry
/// fire, rack, slugs, running dry), ability casts and "ready" pings, heals, the low-health
/// heartbeat, stuns and final elimination. Added to every player at runtime by SfxDirector;
/// it only listens to the player's existing events and state, so gameplay scripts don't
/// need to know about it.
/// </summary>
public class PlayerSfx : MonoBehaviour
{
    [Tooltip("Ground covered per footstep (world units)")]
    public float strideLength = 1.7f;
    [Tooltip("Health fraction where the heartbeat kicks in")]
    public float lowHealthFraction = 0.3f;

    // PlayerMovement3D keeps its vertical speed private; read it so jumps and landings are exact
    static readonly FieldInfo VerticalVelocity =
        typeof(PlayerMovement3D).GetField("verticalVelocity", BindingFlags.Instance | BindingFlags.NonPublic);

    PlayerMovement3D move;
    CharacterController controller;
    AmmoControl ammo;
    WizardAbilityController ability;
    PlayerHealthControl health;
    StatusEffects status;

    float stride;
    float prevVy;
    bool wasAirborne;
    float lowestVy;
    WeaponData prevGun;
    int prevAmmo;
    bool abilityWasReady = true;
    float prevHealth = -1f;
    float prevStun;
    float lastDryFire;
    int firedFrame = -1;

    Vector3 Pos => transform.position;

    void Start()
    {
        move = GetComponent<PlayerMovement3D>();
        controller = GetComponent<CharacterController>();
        health = GetComponent<PlayerHealthControl>();
        status = GetComponent<StatusEffects>();
        ammo = GetComponentInChildren<AmmoControl>(true);
        ability = GetComponentInChildren<WizardAbilityController>(true);

        if (move != null) move.OnDash += OnDash;
        if (ammo != null) { ammo.OnReloaded += OnReloaded; ammo.OnFired += OnFired; }
        if (ability != null) ability.OnAbilityActivated += OnCast;
        if (health != null)
        {
            health.OnHealthChanged += OnHealthChanged;
            health.OnDeath += OnDeath;
            prevHealth = health.currentHealth;
        }
        stride = strideLength * 0.6f;
    }

    void OnDestroy()
    {
        if (move != null) move.OnDash -= OnDash;
        if (ammo != null) { ammo.OnReloaded -= OnReloaded; ammo.OnFired -= OnFired; }
        if (ability != null) ability.OnAbilityActivated -= OnCast;
        if (health != null)
        {
            health.OnHealthChanged -= OnHealthChanged;
            health.OnDeath -= OnDeath;
        }
    }

    void LateUpdate()
    {
        if (move == null || move.gamepad == null) return;   // not set up yet (or a menu dummy)
        Movement();
        Guns();
        Ability();
        Stun();
    }

    // ---------- Movement ----------

    void Movement()
    {
        float vy = VerticalVelocity != null ? (float)VerticalVelocity.GetValue(move)
                 : controller != null ? controller.velocity.y : 0f;
        bool airborne = move.IsAirborne;

        // Jump: vertical speed kicked upward this frame (a second one in the air is the double jump)
        if (vy > 2f && vy > prevVy + 2.5f)
        {
            if (airborne) Sfx.Play(SfxId.DoubleJump, Pos);
            else Sfx.Play(SfxId.Jump, Pos);
        }

        if (airborne) lowestVy = Mathf.Min(lowestVy, vy);
        else if (wasAirborne)
        {
            // Landing: thump scaled by how fast we came down
            if (lowestVy < -7f) Sfx.Play(SfxId.LandHeavy, Pos, Mathf.Clamp01(-lowestVy / 16f) + 0.3f);
            else if (lowestVy < -2.5f) Sfx.Play(SfxId.Land, Pos, 0.6f);
            lowestVy = 0f;
            stride = strideLength * 0.5f;
        }
        wasAirborne = airborne;
        prevVy = vy;

        // Footsteps: one per stride of ground covered (none while sliding on ice or dashing)
        Vector3 v = controller != null ? controller.velocity : Vector3.zero;
        v.y = 0f;
        float speed = v.magnitude;
        if (!airborne && !move.IsDashing && move.StickMagnitude > 0.15f && speed > 0.8f && move.Traction > 0.6f)
        {
            stride += speed * Time.deltaTime;
            if (stride >= strideLength)
            {
                stride = 0f;
                // heavy guns make heavier steps
                float weight = Mathf.Clamp01(move.HeldWeight / 5f);
                Sfx.Play(SfxId.Footstep, Pos, Mathf.Lerp(0.7f, 1f, weight), Mathf.Lerp(1.08f, 0.88f, weight));
            }
        }
        else if (speed < 0.3f) stride = strideLength * 0.6f;   // the first step comes quickly
    }

    void OnDash() => Sfx.Play(SfxId.Dash, Pos);

    // ---------- Guns ----------

    WeaponClass GunClass => ammo != null && ammo.currentGun != null ? ammo.currentGun.weaponClass : WeaponClass.Rifle;

    void Guns()
    {
        if (ammo == null || ammo.currentGun == null) return;

        if (ammo.currentGun != prevGun)
        {
            if (prevGun != null) Sfx.Gun(GunClass, GunSfx.Draw, Pos);
            prevGun = ammo.currentGun;
            prevAmmo = ammo.ammoCount;
            return;
        }

        // The last round leaves the magazine: a hollow click so you know before you pull again
        // (only when it was fired: loading slugs dumps the magazine too)
        if (prevAmmo > 0 && ammo.ammoCount == 0 && firedFrame == Time.frameCount && !ammo.FreeAmmo)
            Sfx.Play(SfxId.MagEmpty, Pos);
        prevAmmo = ammo.ammoCount;

        var pad = ammo.gamepad;
        if (pad == null || GamePause.InputBlocked || ammo.FiringBlocked) return;

        // Dry fire: trigger pulled on an empty gun, or mid magazine reload
        bool shellReload = ammo.currentGun.fireType == "shotgun" || ammo.currentGun.fireType == "grenade";
        bool locked = ammo.ammoCount <= 0 || ammo.IsReloading && !shellReload;
        if (locked && pad.rightTrigger.wasPressedThisFrame && Time.time - lastDryFire > 0.12f)
        {
            lastDryFire = Time.time;
            Sfx.Gun(GunClass, GunSfx.DryFire, Pos);
        }

        // LT on a shotgun: slugs going in
        if (ammo.currentGun.isShotgun && pad.leftTrigger.wasPressedThisFrame && !ammo.FreeAmmo)
            Sfx.Gun(GunClass, GunSfx.SlugLoad, Pos);
    }

    void OnReloaded() => Sfx.Gun(GunClass, GunSfx.ReloadDone, Pos);

    void OnFired(int damage) => firedFrame = Time.frameCount;

    // ---------- Wizard ability ----------

    void OnCast()
    {
        Sfx.Play(CastSound(Elements.Of(ability != null ? ability.wizardData : null)), Pos);
        abilityWasReady = false;
    }

    public static SfxId CastSound(Element element) => element switch
    {
        Element.Fire => SfxId.CastFire,
        Element.Frost => SfxId.CastFrost,
        Element.Water => SfxId.CastWater,
        Element.Lightning => SfxId.CastLightning,
        Element.Earth => SfxId.CastEarth,
        Element.Nature => SfxId.CastNature,
        Element.Poison => SfxId.CastPoison,
        Element.Void => SfxId.CastVoid,
        _ => SfxId.CastArcane,
    };

    void Ability()
    {
        if (ability == null) return;
        bool ready = ability.IsReady;
        if (ready && !abilityWasReady) Sfx.Play(SfxId.AbilityReady, Pos);
        abilityWasReady = ready;
    }

    // ---------- Health ----------

    void OnHealthChanged(float current, float max)
    {
        if (prevHealth >= 0f && max > 0f)
        {
            float gained = current - prevHealth;
            // a respawn refills from (near) nothing to full: that's the spawn shimmer's job, not a heal
            bool respawnRefill = current >= max - 0.01f && gained > max * 0.5f;
            if (gained > 0.5f && prevHealth > 0f && !respawnRefill) Sfx.Play(SfxId.Heal, Pos);

            float low = max * lowHealthFraction;
            if (current > 0f && current < low && prevHealth >= low) Sfx.Play(SfxId.Heartbeat, Pos);
        }
        prevHealth = current;
    }

    void OnDeath() => Sfx.Play(SfxId.Eliminated, Pos);

    // ---------- Stun ----------

    void Stun()
    {
        if (status == null && (status = GetComponent<StatusEffects>()) == null) return;   // added on first status hit
        float s = status.StunRemaining;
        if (s > 0.25f && prevStun <= 0f) Sfx.Play(SfxId.Stun, Pos);
        prevStun = s;
    }
}
