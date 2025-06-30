using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class FireController3D : MonoBehaviour
{
    [Header("References")]
    public GameObject bulletPrefab;   // Your Bullet prefab
    public Transform firePoint;      // The muzzle end Transform

    [Header("Firing Settings")]
    public float bulletSpeed = 20f;  // Initial speed
    public float fireRate = 10f;  // Shots per second
    public float triggerThreshold = 0.1f;

    private float nextAllowedFireTime = 0f;

    [Header("Haptics")]
    public float rumbleLow = 0.2f;   // low-frequency motor (heavy)
    public float rumbleHigh = 0.5f;   // high-frequency motor (buzz)
    public float rumbleDuration = 0.1f;

    void Update()
    {
        var pad = Gamepad.current;
        if (pad == null) return;

        float rt = pad.rightTrigger.ReadValue();

        // Fire code
        if (rt > triggerThreshold && Time.time >= nextAllowedFireTime)
        {
            Shoot();
            nextAllowedFireTime = Time.time + 1f / fireRate;
        }

        // Rumble while trigger is held
        if (rt > triggerThreshold)
        {
            pad.SetMotorSpeeds(rumbleLow, rumbleHigh);
        }
        else
        {
            pad.SetMotorSpeeds(0f, 0f);
        }
    }

    private void OnDisable()
    {
        // Make sure rumble stops if this component is disabled
        if (Gamepad.current != null)
            Gamepad.current.SetMotorSpeeds(0f, 0f);
    }

    private void Shoot()
    {
        // Determine the flat direction
        Vector3 flatDir = firePoint.forward;
        flatDir.y = 0f;
        flatDir.Normalize();

        // Calculate the spawn rotation so the bullet's Z+ faces flatDir
        Quaternion spawnRot = Quaternion.LookRotation(flatDir, Vector3.up);

        // Optionally push the spawn point a bit forward so it doesn't collide with the gun
        Vector3 spawnPos = firePoint.position + flatDir * 0.2f;

        // Instantiate with the correct rotation
        GameObject go = Instantiate(bulletPrefab, spawnPos, spawnRot);

        var bullet = go.GetComponent<Bullet>();
        bullet.speed = -bulletSpeed;
        bullet.Fire(flatDir);
    }

}