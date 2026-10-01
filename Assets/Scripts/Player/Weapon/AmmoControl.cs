using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Firing and ammo for the equipped gun. Every loadout slot keeps its own magazine,
/// so swapping guns never costs a reload — swap back and your rounds are still there.
///
/// Reloads come in two styles:
///  • Magazine (rifles, SMGs, minigun): one timed reload, can't fire until it finishes,
///    rounds already in the mag are kept. Swapping away cancels it.
///  • Shell-by-shell (pump/lever shotguns, grenade launcher): one round at a time;
///    pull the trigger with anything loaded to interrupt and fire. Loaded shells stay.
///
/// Shotguns (LT): chamber a slug. It fires on the next trigger pull ahead of the
/// buckshot (which stays loaded): a light hit on players but a wrecking ball for
/// props, walls, ice walls and crystals. One slug, then a short cooldown.
/// </summary>
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

    [Header("Handling")]
    [Tooltip("Seconds after swapping before the new gun can fire")]
    public float swapDelay = 0.15f;
    [Tooltip("Extra draw time per point of the new gun's weight")]
    public float swapDelayPerWeight = 0.06f;
    [Tooltip("Taking a life reloads the gun in your hands")]
    public bool refillOnKill = true;

    [Header("Shotgun Slugs")]
    [Tooltip("Slugs chambered per LT press")]
    public int slugRounds = 1;
    [Tooltip("Seconds after the last slug fires before you can chamber another")]
    public float slugCooldown = 3.5f;
    [Tooltip("Seconds to rack the slug in before it can fire")]
    public float slugChamberTime = 0.2f;
    [Tooltip("Slug damage to players, as a fraction of a full buckshot volley")]
    public float slugPlayerDamage = 0.4f;
    [Tooltip("Slug damage to props, walls, ice walls and crystals, as a multiple of a full buckshot volley")]
    public float slugStructureDamage = 4f;

    private FireController3D fire;
    private PlayerMovement3D movement;

    /// Raised after each trigger pull that fires (arg: per-projectile damage) — Lightning Reflex
    public event System.Action<int> OnFired;

    /// Lets abilities adjust a shot before it fires (e.g. the scope's focused shot). Returns the new damage.
    public System.Func<int, int> ShotDamageModifier;
    /// Scales weapon spread (the scope steadies your aim)
    public float spreadMultiplier = 1f;

    /// Raised when a reload finishes (magazine full or last shell in) — Fortified
    public event System.Action OnReloaded;

    // Named multipliers from synergies/passives (Overcharge, Forge-Fed, Crashing Wave...)
    readonly System.Collections.Generic.Dictionary<string, float> fireRateMods = new();
    readonly System.Collections.Generic.Dictionary<string, float> reloadSpeedMods = new();
    public float FireRateMultiplier { get; private set; } = 1f;
    public float ReloadSpeedMultiplier { get; private set; } = 1f;

    public void SetFireRateModifier(string key, float mult) => SetMod(fireRateMods, key, mult, v => FireRateMultiplier = v);
    public void SetReloadSpeedModifier(string key, float mult) => SetMod(reloadSpeedMods, key, mult, v => ReloadSpeedMultiplier = v);

    static void SetMod(System.Collections.Generic.Dictionary<string, float> mods, string key, float mult, System.Action<float> apply)
    {
        if (Mathf.Approximately(mult, 1f)) mods.Remove(key); else mods[key] = mult;
        float m = 1f;
        foreach (var v in mods.Values) m *= v;
        apply(Mathf.Max(0.05f, m));
    }

    // Named trigger locks (a wonder weapon owns the trigger) and free-ammo grants (Overdrive Orb)
    readonly System.Collections.Generic.HashSet<string> firingBlocks = new();
    readonly System.Collections.Generic.HashSet<string> freeAmmo = new();

    /// While blocked the gun doesn't fire or reload (another weapon is using the trigger)
    public void SetFiringBlocked(string key, bool blocked)
    {
        if (blocked) { firingBlocks.Add(key); StopReload(); }
        else firingBlocks.Remove(key);
    }

    public bool FiringBlocked => firingBlocks.Count > 0;

    /// Shots cost no ammo and the gun never needs a reload (the magazine is topped up first)
    public void SetFreeAmmo(string key, bool on)
    {
        if (on) { freeAmmo.Add(key); RefillMagazine(); }
        else freeAmmo.Remove(key);
    }

    public bool FreeAmmo => freeAmmo.Count > 0;

    /// The element each shot carries, if not the wizard's own (Elemental Rounds). Null = own.
    public System.Func<Element> ShotElement;

    /// Adds a fraction of the magazine (Tidal Momentum). Doesn't interrupt a reload.
    public void AddAmmoFraction(float fraction)
    {
        if (currentGun == null) return;
        int add = Mathf.Max(1, Mathf.RoundToInt(currentGun.ammoCapacity * fraction));
        ammoCount = Mathf.Min(currentGun.ammoCapacity, ammoCount + add);
        UpdateAmmoBar();
    }

    public int currentGunIndex;
    public WeaponData currentGun;
    public int ammoCount;
    public float nextFireTime;
    public GameObject currentAmmoPrefab;

    public bool IsReloading => reloadCoroutine != null;
    /// 0..1 progress of a magazine reload (shell reloads show the rounds themselves)
    public float ReloadProgress { get; private set; }

    private Coroutine reloadCoroutine;
    private readonly System.Collections.Generic.List<WeaponData> runtimeGuns = new();

    // Per-slot state that survives swapping
    private int[] magazines;
    private int[] slugsChambered;
    private float[] slugReadyTime;
    private bool isSetup;
    private WeaponAbilityControl abilityBar;

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

        magazines = new int[guns.Length];
        slugsChambered = new int[guns.Length];
        slugReadyTime = new float[guns.Length];
        abilityBar = WeaponAbilityControl.FindFor(this);
        RefillAll();
        isSetup = true;

        int start = gunControl != null ? gunControl.currentGunIndex : 0;
        if (start < 0 || start >= guns.Length || guns[start] == null) start = FirstValidIndex();
        if (start >= 0) Equip(start, instant: true);
    }

    void OnEnable() => DamageEvents.Killed += HandleKilled;
    void OnDisable() => DamageEvents.Killed -= HandleKilled;

    // Rewarding kills: a fresh magazine to keep the fight rolling (players only, so a
    // zombie horde can't become infinite ammo)
    void HandleKilled(GameObject victim, GameObject attacker, Vector3 at)
    {
        if (!refillOnKill || attacker == null || victim == null || victim == attacker) return;
        if (movement == null || attacker != movement.gameObject) return;
        if (victim.GetComponent<PlayerHealthControl>() == null) return;
        RefillMagazine();
    }

    /// Total weight of every gun carried
    public int PackWeight
    {
        get
        {
            int w = 0;
            if (guns != null) foreach (var g in guns) if (g != null) w += Mathf.Max(0, g.weight);
            return w;
        }
    }

    void OnDestroy()
    {
        foreach (var gun in runtimeGuns)
            if (gun != null) Destroy(gun);
        runtimeGuns.Clear();
    }

    void Update()
    {
        if (!isSetup || gamepad == null || fire == null || currentGun == null) return;
        if (GamePause.InputBlocked) return;

        if (gunControl != null && currentGunIndex != gunControl.currentGunIndex)
            OnGunEquipped(gunControl.currentGunIndex);

        if (FiringBlocked) return;

        float now = Time.time;

        if (currentGun.isShotgun)
        {
            // LT: chamber a slug (off cooldown), or press again to unload it unspent
            if (gamepad.leftTrigger.wasPressedThisFrame)
            {
                if (IsSlug) UnloadSlug();
                else if (now >= slugReadyTime[currentGunIndex]) ChamberSlug(now);
            }
            // full while ready or loaded, refills after the slug goes off
            abilityBar?.ReportCooldown(IsSlug ? 0f : slugReadyTime[currentGunIndex], slugCooldown);
        }

        if (IsSlug)
        {
            // A chambered slug fires ahead of the buckshot, even mid-reload (the reload carries on)
            if (now >= nextFireTime && TriggerPulled())
            {
                FireCurrentGun();
                if (--slugsChambered[currentGunIndex] <= 0)
                {
                    slugReadyTime[currentGunIndex] = now + slugCooldown;
                    RefreshAmmoPrefab();
                }
                nextFireTime = now + 1f / Mathf.Max(0.01f, currentGun.attackSpeed * FireRateMultiplier);
            }
        }
        else
        {
            // Magazine reloads lock the gun; shell reloads can be interrupted by firing
            bool canFire = ammoCount > 0 && now >= nextFireTime && (!IsReloading || IsShellReload(currentGun));

            if (canFire && TriggerPulled())
            {
                StopReload();
                FireCurrentGun();
                if (!FreeAmmo) ammoCount--;
                nextFireTime = now + 1f / Mathf.Max(0.01f, currentGun.attackSpeed * FireRateMultiplier);
                UpdateAmmoBar();
            }
        }

        if (ammoCount <= 0 && !IsReloading && !FreeAmmo)
            StartReload();

        // Manual reload (X) tops up; rounds already loaded are kept
        if (gamepad.buttonWest.wasPressedThisFrame && !IsReloading && ammoCount < currentGun.ammoCapacity)
            StartReload();
    }

    bool TriggerPulled()
    {
        switch (FireType)
        {
            case "shotgunA":
            case "auto":
                return gamepad.rightTrigger.ReadValue() > 0.1f;
            default: // shotgun, grenade, semi
                return gamepad.rightTrigger.wasPressedThisFrame;
        }
    }

    string FireType => IsSlug ? "semi" : currentGun.fireType;

    bool IsSlug => currentGun.isShotgun && slugsChambered != null && slugsChambered[currentGunIndex] > 0;

    void ChamberSlug(float now)
    {
        slugsChambered[currentGunIndex] = Mathf.Max(1, slugRounds);
        RefreshAmmoPrefab();
        nextFireTime = Mathf.Max(nextFireTime, now + slugChamberTime);
        Rumble.GunAbility(gamepad);
        if (audioSource && currentGun.reloadSound)
            audioSource.PlayOneShot(currentGun.reloadSound);
    }

    void UnloadSlug()
    {
        slugsChambered[currentGunIndex] = 0;
        RefreshAmmoPrefab();
        Rumble.ShellLoaded(gamepad);
    }

    bool HasCustomBullet => wizard != null && wizard.customBulletPrefab != null && !currentGun.megaBomb;

    void FireCurrentGun()
    {
        // Shotgun damage is per pellet; a slug is measured against the full volley:
        // lighter on players, far heavier on cover
        int volley = currentGun.damage * Mathf.Max(1, currentGun.pelletCount);
        int damage = IsSlug ? Mathf.Max(1, Mathf.RoundToInt(volley * slugPlayerDamage)) : currentGun.damage;
        int structureDamage = IsSlug ? Mathf.RoundToInt(volley * slugStructureDamage) : -1;
        if (ShotDamageModifier != null) damage = ShotDamageModifier(damage);

        float spread = currentGun.spreadAngle * spreadMultiplier;
        int projectiles = 1;

        switch (FireType)
        {
            case "shotgun":
            case "shotgunA":
                projectiles = Mathf.Max(1, currentGun.pelletCount);
                break;
            case "grenade":
                spread = 0f;
                break;
            default: // semi (incl. slugs), auto
                if (IsSlug) spread = 0f;
                break;
        }

        Element element = ShotElement != null ? ShotElement() : Element.None;
        for (int i = 0; i < projectiles; i++)
            fire.Shoot(currentAmmoPrefab, spread, currentGun.recoil, damage, structureDamage, element);

        if (fire.firePoint != null)
            BulletFX.MuzzleFlash(fire.Owner, fire.firePoint.position, fire.ShotDirection(),
                BulletFX.ShotPower(IsSlug ? volley : damage * projectiles));

        if (audioSource && currentGun.fireSound)
        {
            audioSource.PlayOneShot(currentGun.fireSound);
            // pump/lever rack after each shot
            if (currentGun.fireType == "shotgun" && ammoCount > 1)
                StartCoroutine(PlayReloadSoundAfter(currentGun.fireSound.length));
        }

        OnFired?.Invoke(damage);
    }

    public void OnGunEquipped(int index)
    {
        if (!isSetup || index == currentGunIndex && currentGun != null) return;
        Equip(index, instant: false);
    }

    void Equip(int index, bool instant)
    {
        if (index < 0 || index >= guns.Length || guns[index] == null) return;

        // Park the old gun's magazine (shells loaded mid-reload are kept)
        StopReload();
        if (currentGun != null && currentGunIndex >= 0 && currentGunIndex < magazines.Length)
            magazines[currentGunIndex] = ammoCount;

        currentGunIndex = index;
        currentGun = guns[index];
        ammoCount = magazines[index];
        RefreshAmmoPrefab();

        // Heavier guns slow you down (and take longer to bring up)
        if (movement != null) movement.SetCarriedWeight(currentGun.weight, PackWeight);

        float draw = swapDelay + swapDelayPerWeight * Mathf.Max(0, currentGun.weight);
        nextFireTime = instant ? Time.time : Mathf.Max(nextFireTime, Time.time + draw);
        UpdateAmmoBar();

        if (ammoCount <= 0) StartReload();
    }

    void RefreshAmmoPrefab()
    {
        if (HasCustomBullet)
            currentAmmoPrefab = wizard.customBulletPrefab;
        else if (IsSlug && currentGun.specialBulletType != null)
            currentAmmoPrefab = currentGun.specialBulletType;
        else
            currentAmmoPrefab = currentGun.baseAmmoType != null ? currentGun.baseAmmoType : currentGun.ammoType;
    }

    /// Instantly fills the equipped gun (Akimbo, Overclock). Cancels any reload.
    public void RefillMagazine()
    {
        if (currentGun == null) return;
        StopReload();
        ammoCount = currentGun.ammoCapacity;
        UpdateAmmoBar();
    }

    /// Fills every gun in the loadout (respawn / match start)
    public void RefillAll()
    {
        if (guns == null || magazines == null) return;
        StopReload();
        for (int i = 0; i < guns.Length; i++)
            magazines[i] = guns[i] != null ? guns[i].ammoCapacity : 0;
        // respawn / match start: no slug chambered, and one ready to go
        if (slugsChambered != null) System.Array.Clear(slugsChambered, 0, slugsChambered.Length);
        if (slugReadyTime != null) System.Array.Clear(slugReadyTime, 0, slugReadyTime.Length);
        if (currentGun != null) RefreshAmmoPrefab();
        if (currentGun != null) ammoCount = currentGun.ammoCapacity;
        UpdateAmmoBar();
    }

    // ---------- Reloading ----------

    static bool IsShellReload(WeaponData gun)
        => gun.fireType == "shotgun" || gun.fireType == "grenade";

    void StartReload()
    {
        if (currentGun == null || ammoCount >= currentGun.ammoCapacity) return;
        StopReload();
        reloadCoroutine = StartCoroutine(IsShellReload(currentGun) ? ReloadShells() : ReloadMagazine());
    }

    void StopReload()
    {
        if (reloadCoroutine != null) StopCoroutine(reloadCoroutine);
        reloadCoroutine = null;
        ReloadProgress = 0f;
        if (ammoBar) ammoBar.color = normalColor;
        UpdateAmmoBar();
    }

    IEnumerator ReloadMagazine()
    {
        if (ammoBar) ammoBar.color = reloadingColor;
        if (audioSource && currentGun.reloadSound)
            audioSource.PlayOneShot(currentGun.reloadSound);

        float duration = Mathf.Max(0.05f, currentGun.reloadTime / ReloadSpeedMultiplier);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            ReloadProgress = Mathf.Clamp01(elapsed / duration);
            SetBarFraction(ReloadProgress);
            yield return null;
        }

        ammoCount = currentGun.ammoCapacity;
        Rumble.ReloadDone(gamepad);
        FinishReload();
    }

    IEnumerator ReloadShells()
    {
        if (ammoBar) ammoBar.color = reloadingColor;

        // A full reload from empty still takes reloadTime
        float perShell = currentGun.reloadTime / Mathf.Max(1, currentGun.ammoCapacity) / ReloadSpeedMultiplier;
        float soundLength = currentGun.reloadSound != null ? currentGun.reloadSound.length : 0f;
        float nextSoundTime = Time.time;

        while (ammoCount < currentGun.ammoCapacity)
        {
            if (audioSource && currentGun.reloadSound && Time.time >= nextSoundTime)
            {
                audioSource.PlayOneShot(currentGun.reloadSound);
                nextSoundTime = Time.time + soundLength;
            }

            yield return new WaitForSeconds(perShell);
            ammoCount++;
            Rumble.ShellLoaded(gamepad);
            UpdateAmmoBar();
        }

        FinishReload();
    }

    void FinishReload()
    {
        OnReloaded?.Invoke();
        reloadCoroutine = null;
        ReloadProgress = 0f;
        if (ammoBar) ammoBar.color = normalColor;
        UpdateAmmoBar();
    }

    // ---------- UI ----------

    void UpdateAmmoBar()
    {
        if (!ammoBar || currentGun == null) return;
        // a magazine reload shows its own progress
        if (reloadCoroutine != null && !IsShellReload(currentGun)) return;
        float pct = currentGun.ammoCapacity > 0 ? (float)ammoCount / currentGun.ammoCapacity : 0f;
        SetBarFraction(pct, ammoCount > 0);
    }

    void SetBarFraction(float pct, bool anyLeft = true)
    {
        if (!ammoBar) return;
        if (pct >= 0.8f) ammoBar.sprite = ammoBarFull;
        else if (pct >= 0.6f) ammoBar.sprite = ammoBar80;
        else if (pct >= 0.4f) ammoBar.sprite = ammoBar60;
        else if (pct >= 0.2f) ammoBar.sprite = ammoBar40;
        else if (pct > 0f && anyLeft) ammoBar.sprite = ammoBar20;
        else ammoBar.sprite = ammoBarEmpty;
    }

    private IEnumerator PlayReloadSoundAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (audioSource && currentGun.reloadSound)
            audioSource.PlayOneShot(currentGun.reloadSound);
    }

    int FirstValidIndex()
    {
        for (int i = 0; i < guns.Length; i++)
            if (guns[i] != null) return i;
        return -1;
    }
}
