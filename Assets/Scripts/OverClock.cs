using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AmmoControl))]
public class OverClock : MonoBehaviour
{
    [Header("Overclock Settings")]
    [Tooltip("How much to multiply the base fire rate")]
    public float fireRateMultiplier = 1.5f;
    [Tooltip("How much to multiply the base recoil")]
    [Range(0f, 1f)]
    public float recoilMultiplier   = 0.5f;
    [Tooltip("Duration of the overclock in seconds")]
    public float duration           = 5f;
    [Tooltip("Cooldown before you can overclock again in seconds")]
    public float cooldown           = 10f;

    private AmmoControl ammoControl;
    private WeaponData currentGun;

    // state
    private bool  isActive;
    private float nextReadyTime;
    private float endTime;

    // to restore
    private float origAttackSpeed;
    private float origRecoil;

    void Awake()
    {
        ammoControl = GetComponent<AmmoControl>();
    }

    void Update()
    {
        if (ammoControl == null) return;
        currentGun = ammoControl.currentGun;
        if (currentGun == null || !currentGun.heavyWeapon) return;

        var pad = Gamepad.current;
        if (!isActive)
        {
            // trigger overclock on LT press, if off cooldown
            if (pad != null && pad.leftTrigger.wasPressedThisFrame 
                && Time.time >= nextReadyTime)
            {
                ActivateOverclock();
            }
        }
        else
        {
            // maintain infinite ammo
            ammoControl.ammoCount = currentGun.ammoCapacity;

            // check for end of duration
            if (Time.time >= endTime)
                DeactivateOverclock();
        }
    }

    private void ActivateOverclock()
    {
        // remember originals
        origAttackSpeed = currentGun.attackSpeed;
        origRecoil      = currentGun.recoil;

        // apply buffs
        currentGun.attackSpeed *= fireRateMultiplier;
        currentGun.recoil      *= recoilMultiplier;

        isActive      = true;
        endTime       = Time.time + duration;
        nextReadyTime = Time.time + cooldown;
    }

    private void DeactivateOverclock()
    {
        // restore
        currentGun.attackSpeed = origAttackSpeed;
        currentGun.recoil      = origRecoil;

        isActive = false;
    }
}
