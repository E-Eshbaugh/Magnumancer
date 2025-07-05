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

    [Header("Controller")]
    public Gamepad gamepad;  // assigned by MultiplayerManager

    private AmmoControl ammoControl;
    private WeaponData  currentGun;

    // state
    public bool  isActive = false;
    private float nextReadyTime;
    private float endTime;

    // to restore
    private float origAttackSpeed;
    private float origRecoil;
    private float origSpreadAngle;

    void Awake()
    {
        ammoControl = GetComponent<AmmoControl>();
        if (ammoControl == null)
            Debug.LogError($"{name}: Missing AmmoControl!");
    }

    void Start()
    {
        // Prevent immediate activation:
        nextReadyTime = Time.time + cooldown;
        Debug.Log($"{name}: Overclock ready at {nextReadyTime:F1}");
    }

    /// <summary>
    /// Call this whenever you swap guns to fully reset overclock state.
    /// </summary>
    public void ResetOverclock()
    {
        if (isActive)
        {
            // restore if needed
            currentGun.attackSpeed = origAttackSpeed;
            currentGun.recoil = origRecoil;
            currentGun.spreadAngle = origSpreadAngle;
        }

        isActive      = false;
        endTime       = 0f;
        nextReadyTime = Time.time + cooldown;
        Debug.Log($"{name}: Overclock reset; next ready at {nextReadyTime:F1}");
    }

    void Update()
    {
        if (gamepad == null)
        {
            Debug.Log($"No gamepad for OverClock");
            return;
        }

        currentGun = ammoControl.currentGun;
        if (currentGun == null || !currentGun.heavyWeapon) return;

        float now = Time.time;
        var   pad = gamepad;

        if (!isActive)
        {
            // only trigger when off cooldown
            if (pad.leftTrigger.wasPressedThisFrame && now >= nextReadyTime)
                ActivateOverclock(now);
        }
        else
        {
            // infinite ammo
            ammoControl.ammoCount = currentGun.ammoCapacity;

            if (now >= endTime)
                DeactivateOverclock();
        }
    }

    void ActivateOverclock(float now)
    {
        origAttackSpeed = currentGun.attackSpeed;
        origRecoil      = currentGun.recoil;
        origSpreadAngle = currentGun.spreadAngle;

        currentGun.attackSpeed *= fireRateMultiplier;
        currentGun.recoil      *= recoilMultiplier;
        currentGun.spreadAngle  = 0f;

        isActive      = true;
        endTime       = now + duration;
        nextReadyTime = now + cooldown;

        Debug.Log($"{name}: Overclock ON until {endTime:F1}s (next ready at {nextReadyTime:F1}s)");
    }

    void DeactivateOverclock()
    {
        currentGun.attackSpeed = origAttackSpeed;
        currentGun.recoil      = origRecoil;
        currentGun.spreadAngle  = origSpreadAngle;
        isActive = false;

        Debug.Log($"{name}: Overclock OFF; next ready at {nextReadyTime:F1}s");
    }
}
