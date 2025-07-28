using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class AmmoControl : MonoBehaviour
{
    [Header("UI")]
    public Image ammoBar;
    public Sprite ammoBarFull, ammoBar80, ammoBar60, ammoBar40, ammoBar20, ammoBarEmpty;

    [Header("Guns")]
    public WeaponData[] guns;          // set by MultiplayerManager OR by GunSwapControl.Setup
    public GunSwapControl gunControl;  // assign in prefab
    public GameObject fireBulletPrefab;
    public GameObject iceBulletPrefab;

    [Header("Controller")]
    public Gamepad gamepad;
    public WizardData wizard;
    public AudioSource audioSource;
    // Internal
    private FireController3D fire;
    public int currentGunIndex;
    public WeaponData currentGun;
    public int ammoCount;
    public float nextFireTime;
    public GameObject currentAmmoPrefab;

    void Awake()
    {
        fire = GetComponent<FireController3D>();
        if (!fire) Debug.LogError($"{name}: No FireController3D found!");
    }

    public void Setup(Gamepad pad, WeaponData[] srcLoadout, WizardData wiz)
    {
        gamepad = pad;
        wizard = wiz;
        guns = srcLoadout != null ? (WeaponData[])srcLoadout.Clone() : new WeaponData[4];
        OnGunEquipped(0);
    }


    void Update()
    {
        if (gamepad == null || fire == null) return;

        // Sync with swaps
        if (currentGunIndex != gunControl.currentGunIndex)
            OnGunEquipped(gunControl.currentGunIndex);

        float now = Time.time;

        // Shotgun toggle
        if (currentGun.isShotgun && gamepad.leftTrigger.wasPressedThisFrame)
        {
            ammoCount = currentGun.ammoCapacity;

            // Swap between base and special bullet — unless wizard overrides
            if (wizard != null && wizard.customBulletPrefab != null)
            {
                currentAmmoPrefab = wizard.customBulletPrefab;
            }
            else
            {
                currentAmmoPrefab = (currentAmmoPrefab == currentGun.baseAmmoType)
                    ? currentGun.specialBulletType
                    : currentGun.baseAmmoType;
            }

            nextFireTime = now;
            UpdateAmmoBar();
        }

        // Fire logic
        if (ammoCount > 0 && now >= nextFireTime)
        {
            bool didFire = false;
            string fireType = currentAmmoPrefab.tag == "slug" ? "semi" : currentGun.fireType;

            GameObject bulletToShoot = (wizard != null && wizard.customBulletPrefab != null)
                ? wizard.customBulletPrefab
                : currentAmmoPrefab;

            switch (fireType)
            {
                case "shotgun":
                    if (gamepad.rightTrigger.wasPressedThisFrame && !audioSource.isPlaying)
                    {
                        for (int i = 0; i < currentGun.pelletCount; i++)
                            fire.Shoot(bulletToShoot, currentGun.spreadAngle, currentGun.recoil);

                        if (audioSource && currentGun.fireSound)
                        {
                            audioSource.PlayOneShot(currentGun.fireSound);
                            if (ammoCount != 1)
                                StartCoroutine(PlayReloadSoundAfter(currentGun.fireSound.length));
                        }

                        didFire = true;
                    }
                    break;

                case "shotgunA":
                    if (gamepad.rightTrigger.ReadValue() > 0.1f)
                    {
                        fire.Shoot(bulletToShoot, currentGun.spreadAngle, currentGun.recoil);
                        audioSource?.PlayOneShot(currentGun.fireSound);
                        didFire = true;
                    }
                    break;

                case "semi":
                    if (gamepad.rightTrigger.wasPressedThisFrame)
                    {
                        fire.Shoot(bulletToShoot, currentGun.spreadAngle, currentGun.recoil);
                        audioSource?.PlayOneShot(currentGun.fireSound);
                        didFire = true;
                    }
                    break;

                case "auto":
                    if (gamepad.rightTrigger.ReadValue() > 0.1f)
                    {
                        fire.Shoot(bulletToShoot, currentGun.spreadAngle, currentGun.recoil);
                        audioSource?.PlayOneShot(currentGun.fireSound);
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

        // Manual reload
        if (gamepad.buttonWest.wasPressedThisFrame && audioSource && currentGun.reloadSound && ammoCount < currentGun.ammoCapacity)
        {
            audioSource.PlayOneShot(currentGun.reloadSound);
            ReloadAmmo();
        }
    }

    public void OnGunEquipped(int index)
    {
        currentGunIndex = index;
        currentGun = guns[index];
        ammoCount = currentGun.ammoCapacity;
        currentAmmoPrefab = (wizard != null && wizard.customBulletPrefab != null)
            ? wizard.customBulletPrefab
            : currentGun.baseAmmoType;

        nextFireTime = Time.time;
        UpdateAmmoBar();
    }

    void ReloadAmmo()
    {
        ammoCount = currentGun.ammoCapacity;
        nextFireTime = Time.time;
        currentAmmoPrefab = (wizard != null && wizard.customBulletPrefab != null)
            ? wizard.customBulletPrefab
            : currentGun.baseAmmoType;
        UpdateAmmoBar();
    }

    void UpdateAmmoBar()
    {
        if (!ammoBar) return;
        float pct = currentGun.ammoCapacity > 0 ? (float)ammoCount / currentGun.ammoCapacity : 0f;

        if (pct >= 0.8f) ammoBar.sprite = ammoBarFull;
        else if (pct >= 0.6f) ammoBar.sprite = ammoBar80;
        else if (pct >= 0.4f) ammoBar.sprite = ammoBar60;
        else if (pct >= 0.2f) ammoBar.sprite = ammoBar40;
        else if (ammoCount > 0) ammoBar.sprite = ammoBar20;
        else ammoBar.sprite = ammoBarEmpty;
    }
    
    private System.Collections.IEnumerator PlayReloadSoundAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (audioSource && currentGun.reloadSound)
            audioSource.PlayOneShot(currentGun.reloadSound);
    }
}
