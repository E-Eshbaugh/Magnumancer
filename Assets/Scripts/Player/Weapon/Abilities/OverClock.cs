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
    public float recoilMultiplier = 0.5f;
    [Tooltip("Duration of the overclock in seconds")]
    public float duration = 5f;
    [Tooltip("Cooldown before you can overclock again in seconds")]
    public float cooldown = 10f;
    public WeaponAbilityControl weaponAbility;

    [Header("Controller")]
    public Gamepad gamepad;  // assigned by MultiplayerManager

    private AmmoControl ammoControl;
    private WeaponData currentGun;

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

    void Start()
    {
        nextReadyTime = Time.time + cooldown;
        Debug.Log($"{name}: Overclock ready at {nextReadyTime:F1}");
    }

    public void ResetOverclock()
    {
        if (isActive && currentGun != null)
        {
            currentGun.attackSpeed = origAttackSpeed;
            currentGun.recoil = origRecoil;
            currentGun.spreadAngle = origSpreadAngle;
        }

        isActive = false;
        endTime = 0f;
        nextReadyTime = Time.time + cooldown;
        Debug.Log($"{name}: Overclock reset; next ready at {nextReadyTime:F1}");
    }

    void Update()
    {
        if (gamepad == null)
        {
            Debug.Log($"{name}: No gamepad assigned.");
            return;
        }

        currentGun = ammoControl.currentGun;
        if (currentGun == null || !currentGun.heavyWeapon) return;

        float now = Time.time;

        if (!isActive)
        {
            if (gamepad.leftTrigger.wasPressedThisFrame && now >= nextReadyTime)
                ActivateOverclock(now);
        }
        else
        {
            ammoControl.ammoCount = currentGun.ammoCapacity;

            if (now >= endTime)
                DeactivateOverclock();
        }
    }

    void ActivateOverclock(float now)
    {
        weaponAbility?.TriggerAbilityFill();

        origAttackSpeed = currentGun.attackSpeed;
        origRecoil = currentGun.recoil;
        origSpreadAngle = currentGun.spreadAngle;

        currentGun.attackSpeed *= fireRateMultiplier;
        currentGun.recoil *= recoilMultiplier;
        currentGun.spreadAngle = 0f;

        isActive = true;
        endTime = now + duration;
        nextReadyTime = now + cooldown;

        Debug.Log($"{name}: Overclock ON until {endTime:F1}s (next ready at {nextReadyTime:F1}s)");
    }

    void DeactivateOverclock()
    {
        currentGun.attackSpeed = origAttackSpeed;
        currentGun.recoil = origRecoil;
        currentGun.spreadAngle = origSpreadAngle;

        isActive = false;
        Debug.Log($"{name}: Overclock OFF; next ready at {nextReadyTime:F1}s");
    }
}
