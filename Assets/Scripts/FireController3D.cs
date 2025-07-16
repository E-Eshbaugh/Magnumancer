using UnityEngine;
using UnityEngine.InputSystem;

public class FireController3D : MonoBehaviour
{
    [Header("Controller")]
    public Gamepad gamepad;                // set by MultiplayerManager

    [Header("Bullet Setup")]
    public Transform firePoint;            // your muzzle or placeholder
    public float     bulletSpeed = 20f;

    void Awake()
    {
        if (firePoint == null)
            Debug.LogError($"{name}: firePoint is not assigned!");
    }

    /// <summary>
    /// Spawns prefabToUse and propels it down firePoint.forward (with optional spread).
    /// </summary>
    public void Shoot(GameObject prefabToUse, float spreadAngle)
    {
        if (prefabToUse == null || firePoint == null)
        {
            Debug.LogError($"{name}: Missing prefab or firePoint in Shoot()");
            return;
        }

        // 1) Compute the flat shooting direction
        Vector3 dir = firePoint.up;
        if (spreadAngle > 0f)
            dir = Quaternion.AngleAxis(
                Random.Range(-spreadAngle, spreadAngle),
                Vector3.up    // spread around the horizontal plane
            ) * dir;

        // 2) Build a rotation so that the bullet's LOCAL upward axis (its "nose") aligns with dir
        //    If your bullet’s model “nose” points along its local Y+, use FromToRotation(Vector3.up, dir).
        //    If it points along its local Z+, use FromToRotation(Vector3.forward, dir).
        Quaternion rot = Quaternion.FromToRotation(Vector3.forward, dir);

        // 3) Instantiate with that rotation
        var proj = Instantiate(prefabToUse, firePoint.position, rot);

        // 4) Drive movement
        if (proj.TryGetComponent<Bullet>(out var bulletComp))
        {
            bulletComp.Initialize(dir);
        }
        else if (proj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = dir * bulletSpeed;
        }
    }

}
