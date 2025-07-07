using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class AmmoControl : MonoBehaviour
{
    [Header("UI")]
    public Image ammoBar;
    public Sprite ammoBarFull, ammoBar80, ammoBar60, ammoBar40, ammoBar20, ammoBarEmpty;

    [Header("Guns")]
    public WeaponData[]   guns;
    public GunSwapControl gunControl;

    [Header("Controller")]
    public Gamepad gamepad;   // set by MultiplayerManager

    // Internal state
    private FireController3D fire;
    public int        currentGunIndex;
    public WeaponData currentGun;
    public int        ammoCount;
    private float      nextFireTime;
    private GameObject currentAmmoPrefab;

    void Awake()
    {
        fire = GetComponent<FireController3D>();
        if (fire == null)
            Debug.LogError($"{name}: No FireController3D found!");
    }

    void Start()
    {
        OnGunEquipped(gunControl.currentGunIndex);
    }

    void Update()
    {
        if (gamepad == null || fire == null) return;

        float now = Time.time;
        var pad  = gamepad;

        // Detect swap
        if (gunControl.currentGunIndex != currentGunIndex)
            OnGunEquipped(gunControl.currentGunIndex);

        // 2) Slug‐round toggle on LT (press once to switch to slug, next press or next swap returns to base)
        if (currentGun.isShotgun && pad.leftTrigger.wasPressedThisFrame)
        {
            ammoCount = currentGun.ammoCapacity;
            // Flip between base and special
            if (currentAmmoPrefab == currentGun.baseAmmoType)
                currentAmmoPrefab = currentGun.specialBulletType;   // slug
            else
                currentAmmoPrefab = currentGun.baseAmmoType;         // back to shells

            Debug.Log($"{name}: Toggled shotgun ammo to '{currentAmmoPrefab.name}'");
            // Reset cooldown so you can shoot right away if you like
            nextFireTime = now;
            UpdateAmmoBar();
        }


        // Fire if ready
        if (ammoCount > 0 && now >= nextFireTime)
        {
            bool didFire = false;

            string fireType = currentGun.fireType;
            if (currentAmmoPrefab.tag == "slug")
            {
                fireType = "semi";
            }

            switch (fireType)
                {
                    case "shotgun":
                        if (pad.rightTrigger.wasPressedThisFrame)
                        {
                            for (int i = 0; i < currentGun.pelletCount; i++)
                                fire.Shoot(currentAmmoPrefab, currentGun.spreadAngle);
                            didFire = true;
                        }
                        break;

                    case "semi":
                        if (pad.rightTrigger.wasPressedThisFrame)
                        {
                            fire.Shoot(currentAmmoPrefab, currentGun.spreadAngle);
                            didFire = true;
                        }
                        break;

                    case "auto":
                        if (pad.rightTrigger.ReadValue() > 0.1f)
                        {
                            fire.Shoot(currentAmmoPrefab, currentGun.spreadAngle);
                            didFire = true;
                        }
                        break;
                }

            if (didFire)
            {
                ammoCount--;
                nextFireTime = now + 1f / currentGun.attackSpeed;
                UpdateAmmoBar();
            }
        }
    }

    public void OnGunEquipped(int index)
    {
        currentGunIndex   = index;
        currentGun        = guns[index];
        ammoCount         = currentGun.ammoCapacity;
        currentAmmoPrefab = currentGun.baseAmmoType;
        nextFireTime      = Time.time;
        Debug.Log($"{name}: Equipped '{currentGun.name}' (base ammo: '{currentAmmoPrefab.name}')");
        //UpdateAmmoBar();
    }

    void UpdateAmmoBar()
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
