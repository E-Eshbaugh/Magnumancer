using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;
using Unity.VisualScripting;

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
    private PlayerMovement3D movement;

    /// Raised after each trigger pull that fires (arg: per-projectile damage) — Lightning Reflex
    public event System.Action<int> OnFired;
    public int currentGunIndex;
    public WeaponData currentGun;
    public int ammoCount;
    public float nextFireTime;
    public GameObject currentAmmoPrefab;
    private bool isReloading = false;
    private Coroutine reloadCoroutine;
    private readonly System.Collections.Generic.List<WeaponData> runtimeGuns = new();

    void Awake()
    {
        fire = GetComponent<FireController3D>();
        movement = GetComponentInParent<PlayerMovement3D>();
        if (!fire) Debug.LogError($"{name}: No FireController3D found!");
    }

    public void Setup(Gamepad pad, WeaponData[] srcLoadout, WizardData wiz)
    {
        gamepad = pad;
        wizard = wiz;
        guns = srcLoadout != null ? (WeaponData[])srcLoadout.Clone() : new WeaponData[4];

        // Give this player their own runtime copy of each weapon so abilities
        // like OverClock can tweak stats without editing the shared asset.
        for (int i = 0; i < guns.Length; i++)
        {
            if (guns[i] == null) continue;
            guns[i] = Instantiate(guns[i]);
            runtimeGuns.Add(guns[i]);
        }

        OnGunEquipped(0);
    }

    void OnDestroy()
    {
        foreach (var gun in runtimeGuns)
            if (gun != null) Destroy(gun);
        runtimeGuns.Clear();
    }

    void Update()
    {
        if (gamepad == null || fire == null || isReloading) return;

        if (currentGunIndex != gunControl.currentGunIndex)
            OnGunEquipped(gunControl.currentGunIndex);

        float now = Time.time;

        // Shotgun ammo swap (LT): switching shell type means reloading with the new shells.
        // Wizards with a custom bullet have nothing to swap to.
        bool hasCustomBullet = wizard != null && wizard.customBulletPrefab != null && !currentGun.megaBomb;
        if (currentGun.isShotgun && !hasCustomBullet && gamepad.leftTrigger.wasPressedThisFrame)
        {
            currentAmmoPrefab = (currentAmmoPrefab == currentGun.baseAmmoType)
                ? currentGun.specialBulletType
                : currentGun.baseAmmoType;

            ammoCount = 0;
            UpdateAmmoBar();
            if (reloadCoroutine != null) StopCoroutine(reloadCoroutine);
            reloadCoroutine = StartCoroutine(ReloadAmmoIncremental());
            return;
        }

        if (ammoCount > 0 && now >= nextFireTime)
        {
            bool didFire = false;
            bool isSlug = currentAmmoPrefab.CompareTag("slug");
            string fireType = isSlug ? "semi" : currentGun.fireType;

            // Shotgun damage is per pellet; a slug hits as hard as the full volley
            int damage = isSlug ? currentGun.damage * Mathf.Max(1, currentGun.pelletCount) : currentGun.damage;

            GameObject bulletToShoot = (wizard != null && wizard.customBulletPrefab != null && !currentGun.megaBomb)
                ? wizard.customBulletPrefab
                : currentAmmoPrefab;

            switch (fireType)
            {
                case "shotgun":
                    if (gamepad.rightTrigger.wasPressedThisFrame && !audioSource.isPlaying)
                    {
                        for (int i = 0; i < currentGun.pelletCount; i++)
                            fire.Shoot(bulletToShoot, currentGun.spreadAngle, currentGun.recoil, damage);

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
                        for (int i = 0; i < Mathf.Max(1, currentGun.pelletCount); i++)
                            fire.Shoot(bulletToShoot, currentGun.spreadAngle, currentGun.recoil, damage);
                        audioSource?.PlayOneShot(currentGun.fireSound);
                        didFire = true;
                    }
                    break;
                case "grenade":
                    if (gamepad.rightTrigger.wasPressedThisFrame)
                    {
                        fire.Shoot(bulletToShoot, 0f, currentGun.recoil, damage);
                        audioSource?.PlayOneShot(currentGun.fireSound);
                        didFire = true;
                    }
                    break;
                case "semi":
                    if (gamepad.rightTrigger.wasPressedThisFrame)
                    {
                        fire.Shoot(bulletToShoot, currentGun.spreadAngle, currentGun.recoil, damage);
                        audioSource?.PlayOneShot(currentGun.fireSound);
                        didFire = true;
                    }
                    break;
                case "auto":
                    if (gamepad.rightTrigger.ReadValue() > 0.1f)
                    {
                        fire.Shoot(bulletToShoot, currentGun.spreadAngle, currentGun.recoil, damage);
                        audioSource?.PlayOneShot(currentGun.fireSound);
                        didFire = true;
                    }
                    break;
            }

            if (didFire)
            {
                OnFired?.Invoke(damage);
                ammoCount--;
                nextFireTime = now + 1f / currentGun.attackSpeed;
                UpdateAmmoBar();
            }
        }
        else if (ammoCount == 0)
        {
            if (reloadCoroutine != null)
                StopCoroutine(reloadCoroutine);
            reloadCoroutine = StartCoroutine(ReloadAmmoIncremental());
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

        // Heavier guns slow you down
        if (movement != null) movement.SetCarriedWeight(currentGun != null ? currentGun.weight : 0);
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

            // Akimbo/OverClock can refill the mag mid-reload; never go past capacity
            if (ammoCount >= currentGun.ammoCapacity) break;
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
