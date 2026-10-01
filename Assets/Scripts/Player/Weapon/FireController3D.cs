using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class FireController3D : MonoBehaviour
{
    [Header("Controller")]
    public Gamepad gamepad;                // set by MultiplayerManager

    [Header("Bullet Setup")]
    public Transform firePoint;            // your muzzle or placeholder
    public float bulletSpeed = 20f;

    [Tooltip("Scales recoil (controller kick) — Stonebind halves it")]
    public float recoilMultiplier = 1f;

    [Tooltip("From high ground, shots angle down to reach the floor this far out")]
    public float highGroundAimDistance = 7f;

    PlayerMovement3D ownerMovement;

    // Player this gun belongs to (credited with bullet/grenade damage)
    public GameObject Owner { get; private set; }

    public void Setup(Gamepad pad)
    {
        gamepad = pad;
    }

    void Awake()
    {
        if (firePoint == null)
            Debug.LogError($"{name}: firePoint is not assigned!");

        Owner = OwnerPlayer();
        ownerMovement = Owner != null ? Owner.GetComponent<PlayerMovement3D>() : null;
    }

    /// Where a shot goes before spread: straight out of the muzzle, angled down when
    /// the shooter stands on high ground. The laser sight uses this too.
    public Vector3 ShotDirection()
    {
        Vector3 dir = firePoint != null ? firePoint.up : transform.forward;
        float elevation = ownerMovement != null ? ownerMovement.elevation : 0f;
        if (elevation > 0.3f)
        {
            Vector3 flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude > 1e-4f)
                dir = (flat.normalized * highGroundAimDistance - Vector3.up * elevation).normalized;
        }
        return dir;
    }

    /// <summary>
    /// Spawns prefabToUse and propels it down firePoint.forward (with optional spread).
    /// damage (if >= 0) overrides the projectile's own damage with the weapon's stat.
    /// structureDamage (if >= 0) is what it does to props and walls instead (slugs).
    /// </summary>
    public void Shoot(GameObject prefabToUse, float spreadAngle, float recoil, int damage = -1, int structureDamage = -1)
    {
        if (prefabToUse == null || firePoint == null)
        {
            Debug.LogError($"{name}: Missing prefab or firePoint in Shoot()");
            return;
        }

        recoil *= recoilMultiplier;

        Rumble.Fire(gamepad, recoil);

        // 1) Compute the shooting direction (angled down from high ground)
        Vector3 dir = ShotDirection();
        if (spreadAngle > 0f)
            dir = Quaternion.AngleAxis(
                Random.Range(-spreadAngle, spreadAngle),
                Vector3.up
            ) * dir;

        // 2) Build rotation from local Z+ (bullet forward) to direction
        Quaternion rot = Quaternion.FromToRotation(Vector3.forward, dir);

        // 3) Instantiate and rotate
        var proj = Instantiate(prefabToUse, firePoint.position, rot);

        // 4) Drive movement
        if (proj.TryGetComponent<Bullet>(out var bulletComp))
        {
            if (damage >= 0) bulletComp.damage = damage;
            bulletComp.structureDamage = structureDamage;
            bulletComp.owner = Owner;
            bulletComp.Initialize(dir);
        }
        else if (proj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = dir * bulletSpeed;
        }

        // Grenade-launcher rounds: the weapon's damage is the blast's max damage
        if (proj.TryGetComponent<GrenadeExplodeAfterDelay>(out var timeGrenade))
        {
            timeGrenade.owner = Owner;
            if (damage >= 0) timeGrenade.maxDamage = damage;
        }
    }

    // The player object this component belongs to (Unity-null safe)
    GameObject OwnerPlayer()
    {
        var player = GetComponentInParent<PlayerMovement3D>();
        return player != null ? player.gameObject : null;
    }
}
