using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Base for the wonder weapons (rare drops). One replaces whatever gun you're holding:
/// it takes the trigger, your gun disappears and the wonder weapon floats where it was,
/// with a pip per shot left. It's gone when its ammo runs out or you lose a life; then
/// your own guns come back exactly as they were.
/// </summary>
public abstract class WonderWeapon : MonoBehaviour
{
    const string BlockKey = "wonder";

    protected ItemBook.Def def;
    protected Color color;
    protected AmmoControl ammo;
    protected FireController3D fire;
    protected PlayerMovement3D movement;
    protected GunOrbitController orbit;

    public int Ammo { get; protected set; }
    public abstract int MaxAmmo { get; }
    /// Seconds between shots
    protected virtual float Cooldown => 0.8f;
    /// Hold the trigger to keep firing (otherwise one shot per pull)
    protected virtual bool Automatic => false;

    protected float nextFire;
    protected Transform model;
    float swingT;   // recoil kick on the model

    Transform gunHolder, hiddenGun;
    readonly List<Renderer> hiddenRenderers = new();
    readonly List<GameObject> pips = new();
    Light glow;
    bool ending;

    protected Gamepad Pad => movement != null ? movement.gamepad : null;
    protected Vector3 Aim => AbilityKit.AimDir(gameObject);
    protected Vector3 Muzzle => model != null ? model.position + Aim * 0.45f : AbilityKit.Chest(gameObject);

    public static T Give<T>(GameObject player, ItemBook.Def def) where T : WonderWeapon
    {
        foreach (var w in player.GetComponents<WonderWeapon>()) w.End(silent: true);
        var weapon = player.AddComponent<T>();
        weapon.Init(def);
        return weapon;
    }

    void Init(ItemBook.Def d)
    {
        def = d;
        color = d.color;
        movement = GetComponent<PlayerMovement3D>();
        ammo = GetComponentInChildren<AmmoControl>();
        fire = GetComponentInChildren<FireController3D>();
        orbit = GetComponentInChildren<GunOrbitController>();
        var swap = GetComponentInChildren<GunSwapControl>();
        gunHolder = swap != null ? swap.gunHolder : null;

        Ammo = MaxAmmo;
        nextFire = Time.time + 0.25f;
        if (ammo != null) ammo.SetFiringBlocked(BlockKey, true);
        DamageEvents.Killed += OnKilled;

        model = new GameObject($"Wonder ({d.name})").transform;
        ItemVisuals.Model(d.id, model);
        model.localScale = Vector3.one * 1.3f;
        glow = model.gameObject.AddComponent<Light>();
        glow.type = LightType.Point; glow.color = color; glow.range = 3f; glow.intensity = 2f; glow.shadows = LightShadows.None;

        int count = Mathf.Min(MaxAmmo, 8);
        for (int i = 0; i < count; i++)
        {
            var pip = AbilityKit.GlowOrb(color, 0.11f);
            pip.name = "AmmoPip";
            pips.Add(pip);
        }
        OnEquip();
    }

    protected virtual void OnEquip() { }
    protected virtual void OnEnd() { }
    protected abstract void Fire();

    void Update()
    {
        if (ending) return;
        var pad = Pad;
        bool input = pad != null && !GamePause.InputBlocked && !WizardSpawnEffect.IsArriving(transform);
        bool trigger = input && (Automatic ? pad.rightTrigger.ReadValue() > 0.1f : pad.rightTrigger.wasPressedThisFrame);
        Tick(trigger);
    }

    /// Default firing: one shot per pull (or held, if Automatic), Cooldown apart
    protected virtual void Tick(bool trigger)
    {
        if (!trigger || Time.time < nextFire || Ammo <= 0) return;
        nextFire = Time.time + Cooldown;
        Ammo--;
        Fire();
        Kick();
        if (Ammo <= 0) Invoke(nameof(OutOfAmmo), 0.5f);
    }

    protected void Kick(float strength = 1f, bool rumble = true)
    {
        swingT = Mathf.Max(swingT, Mathf.Min(1f, strength));
        if (rumble) Rumble.Play(gameObject, 0.5f * strength, 0.8f * strength, 0.2f);
    }

    protected void OutOfAmmo() => End();

    void LateUpdate()
    {
        if (ending || model == null) return;
        Vector3 aim = Aim;
        Vector3 at = orbit != null ? orbit.transform.position : AbilityKit.Chest(gameObject) + aim * 0.9f;
        swingT = Mathf.MoveTowards(swingT, 0f, Time.deltaTime * 5f);
        model.position = at - aim * 0.25f * swingT;
        model.rotation = Quaternion.LookRotation(aim, Vector3.up) * Quaternion.Euler(-25f * swingT, 0f, 0f);
        glow.intensity = 2f + 2f * swingT;

        // one pip per shot left (big magazines show a fraction)
        Vector3 side = Vector3.Cross(Vector3.up, aim).normalized;
        int lit = MaxAmmo <= pips.Count ? Ammo : Mathf.CeilToInt(Ammo / (float)MaxAmmo * pips.Count);
        for (int i = 0; i < pips.Count; i++)
        {
            pips[i].SetActive(i < lit);
            pips[i].transform.position = at + Vector3.up * 0.55f + side * ((i - (pips.Count - 1) * 0.5f) * 0.16f);
        }

        HideGun();
    }

    // The normal gun stays equipped (its magazine is kept) but out of sight
    void HideGun()
    {
        if (gunHolder == null) return;
        var current = gunHolder.childCount > 0 ? gunHolder.GetChild(gunHolder.childCount - 1) : null;
        if (current != hiddenGun)
        {
            hiddenGun = current;
            if (current != null) hiddenRenderers.AddRange(current.GetComponentsInChildren<Renderer>());
        }
        foreach (var r in hiddenRenderers) if (r != null) r.enabled = false;
    }

    /// Back to your own guns. silent = replaced by another wonder weapon.
    public void End(bool silent = false) => End(silent, destroying: false);

    void End(bool silent, bool destroying)
    {
        if (ending) return;
        ending = true;
        CancelInvoke();
        OnEnd();
        if (ammo != null) ammo.SetFiringBlocked(BlockKey, false);
        foreach (var r in hiddenRenderers) if (r != null) r.enabled = true;
        hiddenRenderers.Clear();
        if (!silent && model != null)
        {
            PowerFx.Sparks(model.position, color, 16, 4f, 0.4f, 0.06f, 0.6f);
            AbilityKit.Shockwave(transform.position, 1.4f, color, 0.3f);
        }
        if (model != null) Destroy(model.gameObject);
        foreach (var p in pips) if (p != null) Destroy(p);
        pips.Clear();
        DamageEvents.Killed -= OnKilled;
        if (!destroying) Destroy(this);
    }

    void OnKilled(GameObject victim, GameObject attacker, Vector3 at)
    {
        if (victim == gameObject) End();
    }

    void OnDisable() { if (!gameObject.activeInHierarchy) End(silent: true); }   // eliminated

    void OnDestroy() => End(silent: true, destroying: true);

    // ---------- helpers for the weapons ----------

    /// Runs a delayed effect somewhere that outlives this weapon (the last shot's effects
    /// still land after it's gone)
    protected Coroutine Run(System.Collections.IEnumerator routine)
        => (DropDirector.Instance != null ? (MonoBehaviour)DropDirector.Instance : this).StartCoroutine(routine);

    /// Living enemies inside a flat cone from `from` along `dir`
    protected List<GameObject> InCone(Vector3 from, Vector3 dir, float range, float coneDegrees)
    {
        var found = new List<GameObject>();
        foreach (var e in AbilityKit.Enemies(from, range, gameObject))
        {
            Vector3 to = e.transform.position - from; to.y = 0f;
            if (to.sqrMagnitude < 0.25f || Vector3.Angle(dir, to) <= coneDegrees * 0.5f) found.Add(e);
        }
        return found;
    }

    protected static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }
}
