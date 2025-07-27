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

    public void Setup(Gamepad pad)
    {
        gamepad = pad;
    }

    void Awake()
    {
        if (firePoint == null)
            Debug.LogError($"{name}: firePoint is not assigned!");
    }

    /// <summary>
    /// Spawns prefabToUse and propels it down firePoint.forward (with optional spread).
    /// </summary>
    public void Shoot(GameObject prefabToUse, float spreadAngle, float recoil)
    {
        if (prefabToUse == null || firePoint == null)
        {
            Debug.LogError($"{name}: Missing prefab or firePoint in Shoot()");
            return;
        }

        if (gamepad != null)
        {
            StartCoroutine(HapticRecoil(
                gamepad,
                Mathf.Clamp01(recoil * 0.7f),
                Mathf.Clamp01(recoil * 1.5f)
            ));
        }

        // 1) Compute the flat shooting direction
        Vector3 dir = firePoint.up;
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
            bulletComp.Initialize(dir);
        }
        else if (proj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.linearVelocity = dir * bulletSpeed;
        }
    }

    private IEnumerator HapticRecoil(Gamepad pad, float low, float high)
    {
        if (pad == null) yield break;
        pad.SetMotorSpeeds(low * 1.2f, high * 1.5f);
        yield return new WaitForSeconds(0.05f);
        pad.SetMotorSpeeds(low * 0.5f, high * 0.7f);
        yield return new WaitForSeconds(0.1f);
        pad.SetMotorSpeeds(0f, 0f);
    }
}
