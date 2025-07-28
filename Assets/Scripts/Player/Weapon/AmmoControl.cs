using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

public class AmmoControl : MonoBehaviour
{
    [Header("UI")]
    public Image ammoBar;
    public Sprite ammoBarFull, ammoBar80, ammoBar60, ammoBar40, ammoBar20, ammoBarEmpty;
    public Color normalColor = Color.white;
    public Color reloadingColor = Color.gray;

    [Header("Guns")]
    public WeaponData[] guns;
    public GunSwapControl gunControl;
    public GameObject fireBulletPrefab;
    public GameObject iceBulletPrefab;

    [Header("Controller")]
    public Gamepad gamepad;
    public WizardData wizard;
    public AudioSource audioSource;

    private FireController3D fire;
    public int currentGunIndex;
    public WeaponData currentGun;
    public int ammoCount;
    public float nextFireTime;
    public GameObject currentAmmoPrefab;
    private bool isReloading = false;
    private Coroutine reloadCoroutine;

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
        if (gamepad == null || fire == null || isReloading) return;

        if (currentGunIndex != gunControl.currentGunIndex)
            OnGunEquipped(gunControl.currentGunIndex);

        float now = Time.time;

        if (currentGun.isShotgun && gamepad.leftTrigger.wasPressedThisFrame)
        {
            ammoCount = currentGun.ammoCapacity;
            if (wizard != null && wizard.customBulletPrefab != null && !currentGun.megaBomb)
                currentAmmoPrefab = wizard.customBulletPrefab;
            else
                currentAmmoPrefab = (currentAmmoPrefab == currentGun.baseAmmoType)
                    ? currentGun.specialBulletType
                    : currentGun.baseAmmoType;

            nextFireTime = now;
            UpdateAmmoBar();
        }

        if (ammoCount > 0 && now >= nextFireTime)
        {
            bool didFire = false;
            string fireType = currentAmmoPrefab.tag == "slug" ? "semi" : currentGun.fireType;

            GameObject bulletToShoot = (wizard != null && wizard.customBulletPrefab != null && !currentGun.megaBomb)
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
                case "grenade":
                    if (gamepad.rightTrigger.wasPressedThisFrame)
                    {
                        fire.Shoot(bulletToShoot, 0f, currentGun.recoil);
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

        if (gamepad.buttonWest.wasPressedThisFrame && audioSource && currentGun.reloadSound && ammoCount < currentGun.ammoCapacity)
        {
            ammoCount = 0;
            if (reloadCoroutine != null)
                StopCoroutine(reloadCoroutine);

            reloadCoroutine = StartCoroutine(ReloadAmmoIncremental());
        }
    }

    public void OnGunEquipped(int index)
    {
        if (reloadCoroutine != null)
        {
            StopCoroutine(reloadCoroutine);
            isReloading = false;
            ammoBar.color = normalColor;
            reloadCoroutine = null;
        }

        currentGunIndex = index;
        currentGun = guns[index];
        currentAmmoPrefab = (wizard != null && wizard.customBulletPrefab != null && !currentGun.megaBomb)
            ? wizard.customBulletPrefab
            : currentGun.baseAmmoType;

        nextFireTime = Time.time;
        UpdateAmmoBar();

        // Optional: start reload immediately for new gun if needed
        ammoCount = 0;
        if (ammoCount < currentGun.ammoCapacity && audioSource && currentGun.reloadSound)
            reloadCoroutine = StartCoroutine(ReloadAmmoIncremental());
    }

    private IEnumerator ReloadAmmoIncremental()
    {
        isReloading = true;

        if (ammoBar)
            ammoBar.color = reloadingColor;

        int missingAmmo = currentGun.ammoCapacity - ammoCount;
        float perBulletDelay = currentGun.reloadTime / currentGun.ammoCapacity;

        bool isShotgun = currentGun.fireType == "shotgun" || currentGun.fireType == "grenade";
        float soundLength = currentGun.reloadSound != null ? currentGun.reloadSound.length : 0f;
        float nextSoundTime = 0f;
        float elapsed = 0f;

        if (!isShotgun && audioSource && currentGun.reloadSound)
            audioSource.PlayOneShot(currentGun.reloadSound);

        for (int i = 0; i < missingAmmo; i++)
        {
            if (isShotgun && audioSource && currentGun.reloadSound && elapsed >= nextSoundTime)
            {
                audioSource.PlayOneShot(currentGun.reloadSound);
                nextSoundTime = elapsed + soundLength;
            }

            yield return new WaitForSeconds(perBulletDelay);
            ammoCount++;
            UpdateAmmoBar();
            elapsed += perBulletDelay;
        }

        if (ammoBar)
            ammoBar.color = normalColor;

        isReloading = false;
        reloadCoroutine = null;
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

    private IEnumerator PlayReloadSoundAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (audioSource && currentGun.reloadSound)
            audioSource.PlayOneShot(currentGun.reloadSound);
    }
}
