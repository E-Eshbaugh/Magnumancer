using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class FireController3D : MonoBehaviour
{
    public static FireController3D Instance { get; private set; }

    [Header("Default Bullet Setup")]
    [Tooltip("Fallback bullet prefab if none is provided by the weapon")]
    public GameObject bulletPrefab;
    [Tooltip("Muzzle point of your current weapon")]
    public Transform  firePoint;
    [Tooltip("Speed of the bullet if it has a Rigidbody or a mover script")]
    public float      bulletSpeed = 20f;

    // Rate‐limit timers
    private float nextSemiTime = 0f;
    private float nextAutoTime = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    /// <summary>
    /// Semi‐auto: one shot per press, cooldown by attackSpeed.
    /// </summary>
    public static void semiFire(float attackSpeed, float currentTime, GameObject ammoType, float recoil)
    {
        if (Instance == null) return;

        if (Gamepad.current != null
            && Gamepad.current.rightTrigger.wasPressedThisFrame
            && currentTime >= Instance.nextSemiTime)
        {
            Instance.Shoot(ammoType, recoil);
            Instance.nextSemiTime = currentTime + 1f / attackSpeed;
        }
    }

    /// <summary>
    /// Full‐auto: fires while trigger held, cooldown by attackSpeed.
    /// </summary>
    public static void autoFire(float attackSpeed, float currentTime, GameObject ammoType, float recoil)
    {
        if (Instance == null) return;

        if (Gamepad.current != null
            && Gamepad.current.rightTrigger.ReadValue() > 0.1f
            && currentTime >= Instance.nextAutoTime)
        {
            Instance.Shoot(ammoType, recoil);
            Instance.nextAutoTime = currentTime + 1f / attackSpeed;
        }
    }

    /// <summary>
    /// Internal spawn + recoil logic.
    /// </summary>
    private void Shoot(GameObject ammoType, float recoil)
    {
        // choose the prefab to instantiate
        var prefab = ammoType != null ? ammoType : bulletPrefab;
        if (prefab == null || firePoint == null) return;

        // Determine flat firing direction (XZ plane)
        Vector3 dir = -firePoint.forward;
        dir.y = 0f;
        dir.Normalize();

        // Spawn bullet at the muzzle
        GameObject go = Instantiate(prefab, firePoint.position, Quaternion.LookRotation(dir, Vector3.up));

        // Initialize mover or rigidbody on bullet
        var mover = go.GetComponent<Bullet>();
        if (mover != null)
        {
            mover.Initialize(dir);
        }
        else
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = dir * bulletSpeed;
        }

        // Trigger controller rumble proportional to recoil
        if (Gamepad.current != null && recoil > 0f)
        {
            // map recoil [0..1+] to motor speeds (clamped)
            StartCoroutine(RecoilPulse(Gamepad.current, Mathf.Clamp01(recoil * 0.7f), Mathf.Clamp01(recoil * 1f)));
        }
    }

    private IEnumerator RecoilPulse(Gamepad pad, float baseLow, float baseHigh)
    {
        // Phase 1: sharp punch
        pad.SetMotorSpeeds(
            Mathf.Clamp01(baseLow * 1.2f), 
            Mathf.Clamp01(baseHigh * 1.5f)
        );
        yield return new WaitForSeconds(0.05f); // very brief spike

        // Phase 2: softer tail
        pad.SetMotorSpeeds(baseLow * 0.5f, baseHigh * 0.7f);
        yield return new WaitForSeconds(0.1f);  // linger a bit longer

        // Stop
        pad.SetMotorSpeeds(0f, 0f);
    }

    void OnDisable()
    {
        if (Gamepad.current != null)
            Gamepad.current.SetMotorSpeeds(0f, 0f);
    }
}
