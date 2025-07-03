using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class AmmoControl : MonoBehaviour
{
    [Header("UI")]
    public Image ammoBar;
    public Sprite ammoBarFull, ammoBar80, ammoBar60, ammoBar40, ammoBar20, ammoBarEmpty;

    [Header("Guns")]
    public WeaponData[]    guns;
    public GunSwapControl  gunControl;

    // runtime state
    private int         currentGunIndex;
    public WeaponData  currentGun;
    public int         ammoCount;
    private float       nextFireTime;
    public GameObject toFire;

    void Start()
    {
        currentGunIndex = gunControl.currentGunIndex;
        currentGun = guns[currentGunIndex];
        ammoCount = currentGun.ammoCapacity;
        nextFireTime = 0f;
        currentGun.ammoType = currentGun.baseAmmoType;
        toFire = currentGun.baseAmmoType;

    }

    void Update()
    {
        float currentTime = Time.time;
        var pad = Gamepad.current;
        if (pad == null) return;
        // 4) Pick which prefab to fire
        toFire = currentGun.ammoType;

        // 1) Weapon swap
        if (gunControl.currentGunIndex != currentGunIndex)
        {
            currentGunIndex = gunControl.currentGunIndex;
            currentGun = guns[currentGunIndex];
            ammoCount = currentGun.ammoCapacity;
            nextFireTime = 0f;
        }

        if (ammoCount <= 0)
        {
            currentGun.ammoType = currentGun.baseAmmoType;
        }


        // 2) Shotgun “reload ability” on Left Trigger (use for other special ammo type abilities)
        if (currentGun.isShotgun && pad.leftTrigger.wasPressedThisFrame && currentGun.ammoType == currentGun.baseAmmoType)
        {
            ammoCount = currentGun.ammoCapacity;
            nextFireTime = 0f;
            currentGun.ammoType = currentGun.specialBulletType;
            toFire = currentGun.ammoType;
        }

        // 3) Update UI
        UpdateAmmoBar();

        // 5) Fire logic
        //shotgun fire
        if (currentGun.fireType == "shotgun")
        {
            if (pad.rightTrigger.wasPressedThisFrame
                    && ammoCount > 0
                    && currentTime >= nextFireTime && currentGun.ammoType == currentGun.baseAmmoType)
            {
                FireController3D.shotgunFire(
                    currentGun.attackSpeed,
                    currentTime,
                    toFire,
                    currentGun.recoil,
                    currentGun.pelletCount,
                    currentGun.spreadAngle
                );
                ammoCount--;
                nextFireTime = currentTime + 1f / currentGun.attackSpeed;
            }
            else if (pad.rightTrigger.wasPressedThisFrame
                    && ammoCount > 0
                    && currentTime >= nextFireTime && currentGun.ammoType == currentGun.specialBulletType)
            {
                FireController3D.semiFire(
                    currentGun.attackSpeed,
                    currentTime,
                    toFire,
                    currentGun.recoil
                );
                ammoCount--;
                nextFireTime = currentTime + 1f / currentGun.attackSpeed;
            }
        }
        // Semi-auto
            if (currentGun.fireType == "semi")
            {
                if (pad.rightTrigger.wasPressedThisFrame
                    && ammoCount > 0
                    && currentTime >= nextFireTime)
                {
                    FireController3D.semiFire(
                        currentGun.attackSpeed,
                        currentTime,
                        toFire,
                        currentGun.recoil
                    );
                    ammoCount--;
                    nextFireTime = currentTime + 1f / currentGun.attackSpeed;
                }
            }
            // Full-auto
            else if (currentGun.fireType == "auto")
            {
                if (pad.rightTrigger.ReadValue() > 0.1f
                    && ammoCount > 0
                    && currentTime >= nextFireTime)
                {
                    FireController3D.autoFire(
                        currentGun.attackSpeed,
                        currentTime,
                        toFire,
                        currentGun.recoil
                    );
                    ammoCount--;
                    nextFireTime = currentTime + 1f / currentGun.attackSpeed;
                }
            }
    }

    private void UpdateAmmoBar()
    {
        float pct = currentGun.ammoCapacity > 0
            ? (float)ammoCount / currentGun.ammoCapacity
            : 0f;

        if      (pct >= 0.8f) ammoBar.sprite = ammoBarFull;
        else if (pct >= 0.6f) ammoBar.sprite = ammoBar80;
        else if (pct >= 0.4f) ammoBar.sprite = ammoBar60;
        else if (pct >= 0.2f) ammoBar.sprite = ammoBar40;
        else if (ammoCount > 0) ammoBar.sprite = ammoBar20;
        else                     ammoBar.sprite = ammoBarEmpty;
    }
}
