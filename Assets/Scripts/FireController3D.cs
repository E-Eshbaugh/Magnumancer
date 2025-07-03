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
            Instance.Shoot(ammoType, recoil, applySpray: false);
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
            Instance.Shoot(ammoType, recoil, applySpray: true);
            Instance.nextAutoTime = currentTime + 1f / attackSpeed;
        }
    }

    private void Shoot(GameObject ammoType, float recoil, bool applySpray)
    {
        var prefab = ammoType != null ? ammoType : bulletPrefab;
        if (prefab == null || firePoint == null) return;

        // Determine base direction (flat on XZ)
        Vector3 baseDir = -firePoint.forward; 
        baseDir.y = 0f;
        baseDir.Normalize();

        Vector3 finalDir = baseDir;

        if (applySpray)
        {
            // Only auto‐fire gets spread
            float maxAngle = Mathf.Lerp(0f, 10f, recoil);
            Vector3 yawed = Quaternion.AngleAxis(Random.Range(-maxAngle, maxAngle), Vector3.up) * baseDir;
            finalDir = Quaternion.AngleAxis(Random.Range(-maxAngle, maxAngle), firePoint.right) * yawed;
            finalDir.Normalize();
        }

        // spawn offset so we don't collide with our own gun
        Vector3 spawnPos = firePoint.position + finalDir * 0.5f;
        GameObject go = Instantiate(prefab, spawnPos, Quaternion.LookRotation(finalDir, Vector3.up));

        // try mover, else rb
        var mover = go.GetComponent<Bullet>();
        if (mover != null) mover.Initialize(finalDir);
        else
        {
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = finalDir * bulletSpeed;
        }

        // always rumble
        if (recoil > 0f)
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
