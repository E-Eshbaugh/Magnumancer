using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

/// <summary>
/// SMGs — LT: Akimbo. Refills your mag and draws an off-hand copy with its own
/// magazine; hold LT to fire it (backwards, on purpose). Ends when the off-hand
/// runs dry or you swap guns.
/// </summary>
[RequireComponent(typeof(AmmoControl))]
public class AkimboController : MonoBehaviour
{
    [Header("References")]
    public AmmoControl ammoControl;       // your existing primary‐gun manager
    public Gamepad gamepad;               // assigned by MultiplayerManager
    public Transform secondaryAnchor;     // off‐hand muzzle placeholder
    public GameObject secondaryPrefab;    // off‐hand gun model prefab
    public WeaponAbilityControl weaponAbility; // optional UI feedback

    [Header("Akimbo Settings")]
    public float bulletSpeed = 20f;
    public float fireThreshold = 0.1f;     // LT deadzone
    public float cooldown = 10f;           // before you can re‐Akimbo (counted from activation)

    // state
    public bool akimboActive;
    public int secondaryAmmo;
    public float nextAkimboReadyTime;
    public float nextSecondaryFireTime;

    private GameObject secondaryInstance;
    private GunOrbitController orbit;
    private FireController3D fire;
    private WeaponData akimboGun;
    private int startingSecondaryAmmo = 1;

    public void Setup(Gamepad pad)
    {
        gamepad = pad;
        ammoControl = GetComponent<AmmoControl>();
    }

    void Awake()
    {
        if (ammoControl == null) ammoControl = GetComponent<AmmoControl>();
        fire = GetComponent<FireController3D>();
        orbit = GetComponentInChildren<GunOrbitController>();
        if (orbit == null)
            Debug.LogError($"{name}: No GunOrbitController found!");
    }

    void Update()
    {
        if (ammoControl == null || gamepad == null || orbit == null) return;

        var weapon = ammoControl.currentGun;
        if (akimboActive && weapon != akimboGun) EndAkimbo();
        if (weapon == null || !weapon.akimbo) return;

        float now = Time.time;
        bool inputOk = !GamePause.InputBlocked && !ammoControl.FiringBlocked;   // a wonder weapon has the trigger

        // 1) Activate Akimbo
        if (inputOk && !akimboActive && gamepad.leftTrigger.wasPressedThisFrame && now >= nextAkimboReadyTime)
            StartAkimbo(weapon, now);

        // 2) Fire off‐hand while Akimbo is active
        if (akimboActive)
        {
            if (inputOk && gamepad.leftTrigger.ReadValue() > fireThreshold &&
                now >= nextSecondaryFireTime &&
                secondaryAmmo > 0)
            {
                FireSecondary(weapon);
                secondaryAmmo--;
                nextSecondaryFireTime = now + 1f / Mathf.Max(0.01f, weapon.attackSpeed);

                if (secondaryAmmo <= 0)
                    EndAkimbo();
            }
        }

        // Bar: the off-hand magazine while active, then the cooldown
        if (weaponAbility != null)
        {
            if (akimboActive) weaponAbility.ReportFill((float)secondaryAmmo / startingSecondaryAmmo);
            else weaponAbility.ReportCooldown(nextAkimboReadyTime, cooldown);
        }
    }

    private void StartAkimbo(WeaponData weapon, float now)
    {
        akimboActive = true;
        akimboGun = weapon;
        Rumble.GunAbility(gamepad);
        secondaryAmmo = startingSecondaryAmmo = Mathf.Max(1, weapon.ammoCapacity);
        ammoControl.RefillMagazine();
        nextSecondaryFireTime = 0f;
        nextAkimboReadyTime = now + cooldown;

        if (secondaryInstance == null && secondaryPrefab != null && secondaryAnchor != null)
        {
            secondaryInstance = Instantiate(
                secondaryPrefab,
                secondaryAnchor.position,
                secondaryAnchor.rotation,
                secondaryAnchor
            );
        }
        if (secondaryInstance != null) secondaryInstance.SetActive(true);
    }

    public void EndAkimbo()
    {
        akimboActive = false;
        akimboGun = null;
        secondaryAmmo = 0;
        if (secondaryInstance != null)
            secondaryInstance.SetActive(false);
    }

    private void FireSecondary(WeaponData weapon)
    {
        // Same rounds as the main hand (so wizard bullets carry over)
        var prefab = ammoControl.currentAmmoPrefab != null ? ammoControl.currentAmmoPrefab : weapon.ammoType;
        if (prefab == null || secondaryAnchor == null) return;

        Vector3 dir = -orbit.aimDirection.normalized;

        Vector3 spawnPos = secondaryAnchor.position + dir * 0.5f;
        Quaternion rot = Quaternion.LookRotation(dir, Vector3.up);
        var proj = Instantiate(prefab, spawnPos, rot);

        if (proj.TryGetComponent<Bullet>(out var mover))
        {
            mover.damage = weapon.damage;
            mover.owner = OwnerPlayer();
            mover.Initialize(dir);
        }
        else if (proj.TryGetComponent<Rigidbody>(out var rb))
            rb.linearVelocity = dir * bulletSpeed;

        BulletFX.MuzzleFlash(OwnerPlayer(), spawnPos, dir, BulletFX.ShotPower(weapon.damage));

        if (ammoControl.audioSource && weapon.fireSound)
            ammoControl.audioSource.PlayOneShot(weapon.fireSound);

        float recoil = weapon.recoil * (fire != null ? fire.recoilMultiplier : 1f);
        Rumble.Fire(gamepad, recoil);
    }

    void OnDisable()
    {
        EndAkimbo();
    }

    // The player object this component belongs to (Unity-null safe)
    GameObject OwnerPlayer()
    {
        var player = GetComponentInParent<PlayerMovement3D>();
        return player != null ? player.gameObject : null;
    }
}
