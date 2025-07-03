using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class FireController3D : MonoBehaviour
{
    public static FireController3D Instance { get; private set; }

    [Header("Default Bullet Setup")]
    public GameObject bulletPrefab;
    public Transform  firePoint;
    public float      bulletSpeed = 20f;

    private float nextSemiTime = 0f;
    private float nextAutoTime = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public static void semiFire(float attackSpeed, float currentTime, GameObject ammoType, float recoil)
    {
        if (Instance == null || Gamepad.current == null) return;

        if (Gamepad.current.rightTrigger.wasPressedThisFrame
            && currentTime >= Instance.nextSemiTime)
        {
            // pass applySpray = false for perfectly straight shots
            Instance.Shoot(ammoType, recoil, applySpray: false, spreadAngle: 0f);
            Instance.nextSemiTime = currentTime + 1f / attackSpeed;
        }
    }

    public static void shotgunFire(
        float attackSpeed,
        float currentTime,
        GameObject ammoType,
        float recoil,
        int pelletCount,
        float spreadAngle
    ){
        if (Instance == null || Gamepad.current == null) return;

        if (Gamepad.current.rightTrigger.wasPressedThisFrame 
            && currentTime >= Instance.nextSemiTime)
        {
            for (int i = 0; i < pelletCount; i++)
                Instance.Shoot(ammoType, recoil, applySpray: true,  spreadAngle: spreadAngle);

            Instance.nextSemiTime = currentTime + 1f / attackSpeed;
        }
    }

    public static void autoFire(float attackSpeed, float currentTime, GameObject ammoType, float recoil)
    {
        if (Instance == null || Gamepad.current == null) return;

        if (Gamepad.current.rightTrigger.ReadValue() > 0.1f
            && currentTime >= Instance.nextAutoTime)
        {
            // pass applySpray = true for spread
            Instance.Shoot(ammoType, recoil, applySpray: true,  spreadAngle: 0f);
            Instance.nextAutoTime = currentTime + 1f / attackSpeed;
        }
    }

    private void Shoot(GameObject ammoType, float recoil, bool applySpray, float spreadAngle)
    {
        var prefab = ammoType != null ? ammoType : bulletPrefab;
        if (prefab == null || firePoint == null) return;

        // 1) Base forward direction on XZ
        Vector3 baseDir = -firePoint.forward;
        baseDir.y = 0f;
        baseDir.Normalize();

        // 2) Determine how wide our cone is
        float maxAngle = 0f;
        if (applySpray)
        {
            // If a custom spreadAngle was passed, use that:
            if (spreadAngle > 0f)
                maxAngle = spreadAngle;
            else
                // Otherwise map recoil [0..1] to a reasonable max (e.g. 0°–10°)
                maxAngle = Mathf.Lerp(0f, 10f, recoil);
        }

        // 3) Apply horizontal yaw spread
        Vector3 dir = baseDir;
        if (maxAngle > 0f)
        {
            float yaw = Random.Range(-maxAngle, maxAngle);
            dir = Quaternion.AngleAxis(yaw, Vector3.up) * baseDir;
        }

        // 4) Spawn the bullet in front of the muzzle
        Vector3 spawnPos = firePoint.position + baseDir * 0.5f;
        GameObject go = Instantiate(prefab, spawnPos, Quaternion.LookRotation(dir, Vector3.up));

        // 5) Propel it
        var mover = go.GetComponent<Bullet>();
        if (mover != null)
            mover.Initialize(dir);
        else
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null)
                rb.linearVelocity = dir * bulletSpeed;  // use .velocity, not .linearVelocity
        }

        // 6) Haptic recoil
        if (applySpray && recoil > 0f)
            StartCoroutine(RecoilPulse(
                Gamepad.current,
                Mathf.Clamp01(recoil * 0.7f),
                Mathf.Clamp01(recoil * 1.5f)
            ));
    }


    private IEnumerator RecoilPulse(Gamepad pad, float low, float high)
    {
        pad.SetMotorSpeeds(low * 1.2f, high * 1.5f);
        yield return new WaitForSeconds(0.05f);
        pad.SetMotorSpeeds(low * 0.5f, high * 0.7f);
        yield return new WaitForSeconds(0.1f);
        pad.SetMotorSpeeds(0f, 0f);
    }

    void OnDisable()
    {
        if (Gamepad.current != null)
            Gamepad.current.SetMotorSpeeds(0f, 0f);
    }
}
