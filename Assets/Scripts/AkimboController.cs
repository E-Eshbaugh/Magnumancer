using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class AkimboController : MonoBehaviour
{
    [Header("References")]
    public AmmoControl  ammoControl;            // your existing primary‐gun manager
    public GameObject   secondaryPrefab;        // your off‐hand SMG model prefab
    public Transform    secondaryAnchor;        // empty child at the off‐hand muzzle

    [Header("Akimbo Settings")]
    public float        fireThreshold   = 0.1f; // LT deadzone
    public float        cooldown        = 10f;  // Sec before you can re‐Akimbo
    public float        bulletSpeed     = 20f;

    public bool   akimboActive;
    public int    secondaryAmmo;
    public float  nextAkimboReadyTime;
    public float  nextSecondaryFireTime;

    GameObject secondaryInstance;

    void Update()
    {
        if (ammoControl == null) return;
        var weapon = ammoControl.currentGun;
        if (weapon == null || !weapon.akimbo) return;

        var pad = Gamepad.current;
        if (pad == null) return;

        float now = Time.time;

        // 1) Activate Akimbo on LT tap
        if (!akimboActive 
            && pad.leftTrigger.wasPressedThisFrame
            && now >= nextAkimboReadyTime)
        {
            StartAkimbo(weapon);
        }

        // 2) While Akimbo is active, handle LT → secondary fire
        if (akimboActive)
        {
            if (pad.leftTrigger.ReadValue() > fireThreshold 
                && now >= nextSecondaryFireTime
                && secondaryAmmo > 0)
            {
                FireSecondary(weapon);
                secondaryAmmo--;
                nextSecondaryFireTime = now + 1f / weapon.attackSpeed;

                if (secondaryAmmo <= 0)
                    EndAkimbo();
            }
        }

        // 3) Primary (RT) still fires as normal via AmmoControl.Update()
    }

    private void StartAkimbo(WeaponData weapon)
    {
        akimboActive        = true;
        secondaryAmmo       = weapon.ammoCapacity;
        ammoControl.ammoCount = weapon.ammoCapacity;
        nextSecondaryFireTime = 0f;
        nextAkimboReadyTime = Time.time + cooldown;

        // Spawn & show the off-hand model under the anchor
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
    }

    public void EndAkimbo()
    {
        akimboActive = false;
        if (secondaryInstance != null)
            secondaryInstance.SetActive(false);
    }

    private void FireSecondary(WeaponData weapon)
    {
        // choose ammo prefab
        var prefab = weapon.ammoType;
        if (prefab == null) return;

        // direction flat on XZ
        Vector3 dir = secondaryAnchor.forward;
        dir.y = 0f;
        dir.Normalize();

        // spawn just ahead of off-hand muzzle
        Vector3 spawnPos = secondaryAnchor.position + dir * 0.5f;
        GameObject go = Instantiate(prefab, spawnPos, Quaternion.LookRotation(dir));

        // propel bullet
        if (go.TryGetComponent<Bullet>(out var mover))
            mover.Initialize(dir);
        else if (go.TryGetComponent<Rigidbody>(out var rb))
            rb.linearVelocity = dir * bulletSpeed;

        // haptic rumble
        if (weapon.recoil > 0f)
            StartCoroutine(HapticRecoil(
                Gamepad.current,
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
        if (Gamepad.current != null)
            Gamepad.current.SetMotorSpeeds(0f, 0f);
    }
}
