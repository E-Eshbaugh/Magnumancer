using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class AmmoControl : MonoBehaviour
{
    [Header("- UI -")]
    public Image ammoBar;
    public Sprite ammoBarFull, ammoBar80, ammoBar60, ammoBar40, ammoBar20, ammoBarEmpty;

    [Header("- Guns -")]
    public WeaponData[] guns;
    public int currentGunIndex = 0;
    public GunSwapControl gunControl;

    // runtime state
    private WeaponData currentGun;
    private int        ammoCount;
    private float      nextFireTime;
    private GameObject ammoType;

    void Start()
    {
        // initialize
        currentGunIndex = gunControl.currentGunIndex;
        currentGun = guns[currentGunIndex];
        ammoCount = currentGun.ammoCapacity;
        ammoType = currentGun.ammoType;
    }

    void Update()
    {
        // 1) see if we’ve swapped guns
        if (gunControl.currentGunIndex != currentGunIndex)
        {
            currentGunIndex = gunControl.currentGunIndex;
            currentGun = guns[currentGunIndex];
            ammoCount = currentGun.ammoCapacity;
            ammoType = currentGun.ammoType;
            nextFireTime = 0f;
        }

        // 2) update the UI
        UpdateAmmoBar();

        // 3) read the trigger (0–1)
        var pad = Gamepad.current;
        if (pad == null) return;
        float rt = pad.rightTrigger.ReadValue();
        float now = Time.time;

        // 4) handle firing
        if (currentGun.fireType == "semi")
        {
            if (pad.rightTrigger.wasPressedThisFrame && ammoCount > 0 && now >= nextFireTime)
            {
                FireController3D.semiFire(currentGun.attackSpeed, now, ammoType);
                ammoCount--;
                nextFireTime = now + 1f / currentGun.attackSpeed;
            }
        }
        else if (currentGun.fireType == "auto")
        {
            // full-auto: as long as you hold past a small threshold
            if (rt > 0.1f && ammoCount > 0 && now >= nextFireTime)
            {
                FireController3D.autoFire(currentGun.attackSpeed, now, ammoType);
                ammoCount--;
                nextFireTime = now + 1f / currentGun.attackSpeed;
            }
        }
    }

    private void UpdateAmmoBar()
    {
        float pct = (float)ammoCount / currentGun.ammoCapacity;
        if      (pct >= 0.8f) ammoBar.sprite = ammoBarFull;
        else if (pct >= 0.6f) ammoBar.sprite = ammoBar80;
        else if (pct >= 0.4f) ammoBar.sprite = ammoBar60;
        else if (pct >= 0.2f) ammoBar.sprite = ammoBar40;
        else if (ammoCount > 0) ammoBar.sprite = ammoBar20;
        else                    ammoBar.sprite = ammoBarEmpty;
    }
}
