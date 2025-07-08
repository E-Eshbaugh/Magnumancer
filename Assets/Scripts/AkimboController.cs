using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(AmmoControl))]
public class AkimboController : MonoBehaviour
{
    [Header("References")]
    public AmmoControl    ammoControl;       // your existing primary‐gun manager
    public Gamepad        gamepad;           // assigned by MultiplayerManager
    public Transform      secondaryAnchor;   // off‐hand muzzle placeholder
    public GameObject     secondaryPrefab;   // off‐hand gun model prefab
    public WeaponAbilityControl   weaponAbility;       // optional UI feedback

    [Header("Akimbo Settings")]
    public float bulletSpeed     = 20f;
    public float fireThreshold   = 0.1f;     // LT deadzone
    public float cooldown        = 10f;      // before you can re‐Akimbo

    // state
    public bool   akimboActive;
    public int    secondaryAmmo;
    public float  nextAkimboReadyTime;
    public float  nextSecondaryFireTime;

    private GameObject         secondaryInstance;
    private GunOrbitController orbit;

    void Awake()
    {
        // cache your orbit so you get the precise flat aim
        orbit = GetComponentInChildren<GunOrbitController>();
        if (orbit == null)
            Debug.LogError($"{name}: No GunOrbitController found!");
    }

    void Update()
    {
        if (ammoControl == null || gamepad == null || orbit == null) return;
        var weapon = ammoControl.currentGun;
        if (weapon == null || !weapon.akimbo) return;

        float now = Time.time;
        var   pad = gamepad;

        // 1) Activate Akimbo
        if (!akimboActive && pad.leftTrigger.wasPressedThisFrame && now >= nextAkimboReadyTime)
            StartAkimbo(weapon, now);

        // 2) While Akimbo is active, fire off‐hand on LT hold
        if (akimboActive)
        {
            if (pad.leftTrigger.ReadValue() > fireThreshold 
             && now >= nextSecondaryFireTime
             && secondaryAmmo > 0)
            {
                FireSecondary(weapon, now);
                secondaryAmmo--;
                nextSecondaryFireTime = now + 1f / weapon.attackSpeed;

                if (secondaryAmmo <= 0)
                    EndAkimbo();
            }
        }
    }

    private void StartAkimbo(WeaponData weapon, float now)
    {
        weaponAbility?.TriggerAbilityFill(); // optional UI feedback
        akimboActive         = true;
        secondaryAmmo        = weapon.ammoCapacity;
        ammoControl.ammoCount= weapon.ammoCapacity;
        nextSecondaryFireTime= 0f;
        nextAkimboReadyTime  = now + cooldown;

        if (secondaryInstance == null)
        {
            secondaryInstance = Instantiate(
                secondaryPrefab,
                secondaryAnchor.position,
                secondaryAnchor.rotation,
                secondaryAnchor
            );
        }
        secondaryInstance.SetActive(true);

        Debug.Log($"{name}: Akimbo ON for {weapon.name}, ammo={secondaryAmmo}");
    }

    public void EndAkimbo()
    {
        akimboActive = false;
        if (secondaryInstance != null)
            secondaryInstance.SetActive(false);
        Debug.Log($"{name}: Akimbo OFF; next ready at {nextAkimboReadyTime:F1}s");
    }

    private void FireSecondary(WeaponData weapon, float now)
    {
        var prefab = weapon.ammoType;
        if (prefab == null) return;

        // 1) Use the flat aim direction from orbit
        Vector3 dir = -orbit.aimDirection;

        // 2) Spawn at the off‐hand muzzle
        Vector3 spawnPos = secondaryAnchor.position + dir * 0.5f;
        Quaternion rot   = Quaternion.LookRotation(dir, Vector3.up);
        var proj = Instantiate(prefab, spawnPos, rot);

        // 3) Propel or initialize
        if (proj.TryGetComponent<Bullet>(out var mover))
            mover.Initialize(dir);
        else if (proj.TryGetComponent<Rigidbody>(out var rb))
            rb.linearVelocity = dir * bulletSpeed;

        // 4) Haptic recoil on the correct pad
        StartCoroutine(HapticRecoil(
            gamepad,
            Mathf.Clamp01(weapon.recoil * 0.7f),
            Mathf.Clamp01(weapon.recoil * 1.5f)
        ));
    }

    private IEnumerator HapticRecoil(Gamepad pad, float low, float high)
    {
        pad.SetMotorSpeeds(low * 1.2f, high * 1.5f);
        yield return new WaitForSeconds(0.05f);
        pad.SetMotorSpeeds(low * 0.5f, high * 0.7f);
        yield return new WaitForSeconds(0.1f);
        pad.SetMotorSpeeds(0f, 0f);
    }

    void OnDisable()
    {
        if (gamepad != null)
            gamepad.SetMotorSpeeds(0f, 0f);
    }
}
