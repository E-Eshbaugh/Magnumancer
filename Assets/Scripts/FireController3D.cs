using UnityEngine;
using UnityEngine.InputSystem;

public class FireController3D : MonoBehaviour
{
    public static FireController3D Instance { get; private set; }

    [Header("Bullet Setup")]
    public GameObject bulletPrefab;       // assign your bullet prefab here
    public Transform  firePoint;          // muzzle/exit point of the gun
    public float      bulletSpeed = 20f;  // velocity applied to each bullet

    // internal timers for rate‐limiting
    private float nextSemiTime = 0f;
    private float nextAutoTime = 0f;

    void Awake()
    {
        // singleton pattern
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    /// <summary>
    /// Fires once per trigger press, at a maximum rate of attackSpeed shots/sec.
    /// Call from your Update: FireController3D.semiFire(attackSpeed, Time.time);
    /// </summary>
    public static void semiFire(float attackSpeed, float currentTime, GameObject ammoType)
    {
        // only proceed if our singleton is valid
        if (Instance == null) return;

        // check for a new button‐down event and cooldown
        if (Gamepad.current != null
            && Gamepad.current.rightTrigger.wasPressedThisFrame
            && currentTime >= Instance.nextSemiTime)
        {
            Instance.Shoot(ammoType);
            Instance.nextSemiTime = currentTime + 1f / attackSpeed;
        }
    }

    /// <summary>
    /// Fires repeatedly while holding the trigger, at attackSpeed shots/sec.
    /// Call from your Update: FireController3D.autoFire(attackSpeed, Time.time);
    /// </summary>
    public static void autoFire(float attackSpeed, float currentTime, GameObject ammoType)
    {
        if (Instance == null) return;

        // check for trigger held beyond a small threshold and cooldown
        if (Gamepad.current != null
            && Gamepad.current.rightTrigger.ReadValue() > 0.1f
            && currentTime >= Instance.nextAutoTime)
        {
            Instance.Shoot(ammoType);
            Instance.nextAutoTime = currentTime + 1f / attackSpeed;
        }
    }

    /// <summary>
    /// Spawns the bullet prefab and gives it velocity.
    /// </summary>
    private void Shoot(GameObject ammoType)
    {
        if (bulletPrefab == null || firePoint == null) return;

        bulletPrefab = ammoType;

        // Calculate flat firing direction (XZ plane)
        Vector3 dir = -firePoint.forward;
        dir.y = 0f;
        dir.Normalize();

        // Spawn the bullet
        GameObject go = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(dir));

        // Initialize its mover
        var mover = go.GetComponent<Bullet>();
        if (mover != null)
        {
            mover.Initialize(dir);
        }
        else
        {
            Debug.LogWarning("Bullet prefab needs a Bullet component!");
        }
    }
}
