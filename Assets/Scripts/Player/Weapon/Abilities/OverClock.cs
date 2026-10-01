using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Heavy weapons (Minigun) — LT: Overclock. Faster fire, less recoil, no spread and
/// a bottomless magazine for a few seconds. Ends early if you swap off the gun.
/// </summary>
[RequireComponent(typeof(AmmoControl))]
public class OverClock : MonoBehaviour
{
    [Header("Overclock Settings")]
    [Tooltip("How much to multiply the base fire rate")]
    public float fireRateMultiplier = 1.5f;
    [Tooltip("How much to multiply the base recoil")]
    [Range(0f, 1f)]
    public float recoilMultiplier = 0.5f;
    [Tooltip("Duration of the overclock in seconds")]
    public float duration = 5f;
    [Tooltip("Cooldown before you can overclock again in seconds (counted from activation)")]
    public float cooldown = 10f;
    public WeaponAbilityControl weaponAbility;

    [Header("Controller")]
    public Gamepad gamepad;  // assigned by MultiplayerManager

    private AmmoControl ammoControl;
    private WeaponData boostedGun;

    // state
    private bool isActive = false;
    private float nextReadyTime;
    private float endTime;

    // to restore
    private float origAttackSpeed;
    private float origRecoil;
    private float origSpreadAngle;

    public void Setup(Gamepad pad)
    {
        gamepad = pad;
    }

    void Awake()
    {
        ammoControl = GetComponent<AmmoControl>();
        if (ammoControl == null)
            Debug.LogError($"{name}: Missing AmmoControl!");
    }

    public void ResetOverclock()
    {
        DeactivateOverclock();
        nextReadyTime = 0f;
    }

    void Update()
    {
        var gun = ammoControl.currentGun;

        // Swapped off the overclocked gun: shut it down (stats live on this player's runtime copy)
        if (isActive && gun != boostedGun)
            DeactivateOverclock();

        if (gamepad == null || gun == null || !gun.heavyWeapon) return;

        float now = Time.time;

        if (!isActive)
        {
            if (!GamePause.InputBlocked && !ammoControl.FiringBlocked && gamepad.leftTrigger.wasPressedThisFrame && now >= nextReadyTime)
                ActivateOverclock(gun, now);
            // the bar drained while active, so it refills over what's left of the cooldown
            weaponAbility?.ReportCooldown(nextReadyTime, Mathf.Max(0.01f, cooldown - duration));
        }
        else
        {
            if (ammoControl.ammoCount < gun.ammoCapacity)
                ammoControl.RefillMagazine();

            // drains while active
            weaponAbility?.ReportFill((endTime - now) / duration);

            if (now >= endTime)
                DeactivateOverclock();
        }
    }

    void ActivateOverclock(WeaponData gun, float now)
    {
        boostedGun = gun;
        origAttackSpeed = gun.attackSpeed;
        origRecoil = gun.recoil;
        origSpreadAngle = gun.spreadAngle;

        gun.attackSpeed *= fireRateMultiplier;
        gun.recoil *= recoilMultiplier;
        gun.spreadAngle = 0f;

        isActive = true;
        Rumble.GunAbility(gamepad);
        Rumble.Hold(gamepad, "overclock", 0.06f, 0.14f); // the minigun hums while overclocked
        endTime = now + duration;
        nextReadyTime = now + cooldown;
        ammoControl.RefillMagazine();
    }

    void DeactivateOverclock()
    {
        if (!isActive) return;
        if (boostedGun != null)
        {
            boostedGun.attackSpeed = origAttackSpeed;
            boostedGun.recoil = origRecoil;
            boostedGun.spreadAngle = origSpreadAngle;
        }

        boostedGun = null;
        isActive = false;
        Rumble.Release(gamepad, "overclock");
    }

    void OnDisable() => DeactivateOverclock();
}
