using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Per-target status effects (brands, freeze counters, stuns, void marks) and the
/// Elemental Ecosystem statuses built on them (see ElementStatus): Soaked, Charged,
/// Poisoned and Staggered live here; Burning = brands (or a burn timer), Chilled/Frozen =
/// freeze counters, Rooted = root, Marked = void mark. ElementReactions reads and
/// consumes them. Added on demand to players and monsters via StatusEffects.Of(target).
/// </summary>
public class StatusEffects : MonoBehaviour
{
    [Header("Freeze")]
    public float freezeSlowPerStack = 0.08f;   // 5 stacks = 40% slower
    public float freezeDecayTime = 3f;         // stacks clear this long after the last hit

    // Emberguard brands, tracked per attacker
    readonly Dictionary<GameObject, int> brands = new();
    readonly Dictionary<GameObject, float> lastBrandTime = new();
    public float brandDecayTime = 4f;

    int freezeStacks;
    float lastFreezeTime;

    /// The Hollow who marked this target with a Void Shot (explodes on death / next stock loss)
    public GameObject VoidMarkedBy { get; private set; }

    [Header("Element statuses")]
    public float soakDuration = 5f;
    public float chargeDuration = 3f;
    [Tooltip("Poisoned lingers this long after the last poison tick")]
    public float poisonLinger = 3f;
    public float staggerDuration = 1f;
    [Tooltip("Brands (from anyone, combined) needed to count as Burning")]
    public int brandsForBurning = 2;
    [Tooltip("Bullet buildup toward Soaked/Charged empties this long after the last hit")]
    public float buildupDecayTime = 3f;
    [Tooltip("The hit that freezes someone can't also shatter them")]
    public float frozenSettleTime = 0.12f;

    float soakedUntil, chargedUntil, poisonedUntil, staggeredUntil, burningUntil, rootedUntil;
    float frozenUntil, frozenAt;
    float soakBuild, chargeBuild, lastSoakBuild, lastChargeBuild;

    /// Reactions can't go off on this target again until this time
    public float ReactionCooldownUntil { get; set; }

    PlayerMovement3D movement;
    NavMeshAgent agent;
    float agentBaseSpeed;
    float stunMultiplier = 1f;
    Coroutine stunRoutine;

    public static StatusEffects Of(GameObject target)
    {
        if (target == null) return null;
        var fx = target.GetComponent<StatusEffects>();
        return fx ? fx : target.AddComponent<StatusEffects>();
    }

    void Awake()
    {
        movement = GetComponent<PlayerMovement3D>();
        agent = GetComponent<NavMeshAgent>();
        if (agent) agentBaseSpeed = agent.speed;
    }

    void Update()
    {
        if (freezeStacks > 0 && Time.time - lastFreezeTime > freezeDecayTime)
        {
            freezeStacks = 0;
            ApplySpeed();
        }
    }

    // ---------- Brands ----------
    /// Adds a brand from attacker; returns the new count (brands decay if not refreshed).
    public int AddBrand(GameObject attacker)
    {
        if (lastBrandTime.TryGetValue(attacker, out float last) && Time.time - last > brandDecayTime)
            brands[attacker] = 0;
        brands.TryGetValue(attacker, out int count);
        brands[attacker] = ++count;
        lastBrandTime[attacker] = Time.time;
        ElementStatusFx.On(gameObject);
        return count;
    }

    public void ClearBrands(GameObject attacker) => brands.Remove(attacker);

    /// Live brands from this attacker (0 if they've decayed)
    public int BrandCount(GameObject attacker)
    {
        if (!brands.TryGetValue(attacker, out int n)) return 0;
        if (lastBrandTime.TryGetValue(attacker, out float last) && Time.time - last > brandDecayTime) return 0;
        return n;
    }

    /// Live brands from everyone combined
    public int TotalBrands()
    {
        int total = 0;
        foreach (var kv in brands)
            if (!lastBrandTime.TryGetValue(kv.Key, out float last) || Time.time - last <= brandDecayTime)
                total += kv.Value;
        return total;
    }

    // ---------- Freeze ----------
    /// Adds a freeze counter (max 5) and returns the new count.
    public int AddFreeze()
    {
        freezeStacks = Mathf.Min(freezeStacks + 1, 5);
        lastFreezeTime = Time.time;
        ApplySpeed();
        return freezeStacks;
    }

    public int FreezeStacks => freezeStacks;

    /// Tops freeze counters up to at least n (Brittle); returns the count
    public int FreezeAtLeast(int n)
    {
        while (freezeStacks < Mathf.Min(n, 5)) AddFreeze();
        return freezeStacks;
    }

    /// Can't move for a moment (Thornsnare, Glacial Lance)
    public void Root(float duration)
    {
        rootedUntil = Mathf.Max(rootedUntil, Time.time + duration);
        Stun(0.05f, duration);
    }

    /// Fully frozen for a while (Fractalshot freeze, Flash Freeze encase): Shatter-able
    public void MarkFrozen(float duration)
    {
        frozenAt = Time.time;
        frozenUntil = Mathf.Max(frozenUntil, Time.time + duration);
    }

    public void ClearFreeze()
    {
        freezeStacks = 0;
        ApplySpeed();
    }

    // ---------- Stun ----------
    float stunnedUntil;

    /// Seconds left on the current stun or root (a new Stun replaces it, so reactions
    /// check this to avoid cutting a longer one short)
    public float StunRemaining => Mathf.Max(0f, stunnedUntil - Time.time);

    /// Stuns only if that lengthens the current stun
    public void StunAtLeast(float speedMultiplier, float duration)
    {
        if (StunRemaining < duration) Stun(speedMultiplier, duration);
    }

    /// Slows movement to speedMultiplier for duration (players also get slowed aim via StunEffect).
    public void Stun(float speedMultiplier, float duration)
    {
        stunnedUntil = Time.time + duration;
        Rumble.Stunned(gameObject, duration);
        var playerStun = GetComponent<StunEffect>();
        if (playerStun != null)
        {
            playerStun.ApplyStun(speedMultiplier, speedMultiplier, duration);
            return;
        }
        if (stunRoutine != null) StopCoroutine(stunRoutine);
        stunRoutine = StartCoroutine(StunRoutine(speedMultiplier, duration));
    }

    /// Cuts a stun or root short (Shatter knocks you out of the ice)
    public void EndStun()
    {
        rootedUntil = stunnedUntil = 0f;
        var playerStun = GetComponent<StunEffect>();
        if (playerStun != null) { playerStun.EndStun(); return; }
        if (stunRoutine != null) StopCoroutine(stunRoutine);
        stunRoutine = null;
        stunMultiplier = 1f;
        ApplySpeed();
    }

    IEnumerator StunRoutine(float mult, float duration)
    {
        stunMultiplier = mult;
        ApplySpeed();
        yield return new WaitForSeconds(duration);
        stunMultiplier = 1f;
        ApplySpeed();
        stunRoutine = null;
    }

    // ---------- Void mark ----------
    public void MarkVoid(GameObject caster)
    {
        VoidMarkedBy = caster;
        echoReady = true;
    }
    public void ClearVoidMark() { VoidMarkedBy = null; echoReady = false; }

    // Echo spends the mark's echo, not the mark itself (The Hollow keeps the death burst)
    bool echoReady;

    // ---------- Element statuses ----------
    public bool IsSoaked => Time.time < soakedUntil;
    public bool IsCharged => Time.time < chargedUntil;
    public bool IsPoisoned => Time.time < poisonedUntil;
    public bool IsStaggered => Time.time < staggeredUntil;
    public bool IsRooted => Time.time < rootedUntil;
    public bool IsBurning => Time.time < burningUntil || TotalBrands() >= brandsForBurning;
    public bool IsChilled => freezeStacks > 0;
    public bool IsFrozen =>
        (Time.time < frozenUntil && Time.time - frozenAt >= frozenSettleTime) ||
        (freezeStacks >= 5 && Time.time - lastFreezeTime >= frozenSettleTime);

    public void Soak(float duration = -1f) => Set(ref soakedUntil, duration < 0f ? soakDuration : duration);
    public void Charge(float duration = -1f) => Set(ref chargedUntil, duration < 0f ? chargeDuration : duration);
    public void Poison(float duration = -1f) => Set(ref poisonedUntil, duration < 0f ? poisonLinger : duration);
    public void Stagger(float duration = -1f) => Set(ref staggeredUntil, duration < 0f ? staggerDuration : duration);
    /// On fire without brands (lava, fireball, fire dash)
    public void SetBurning(float duration) => Set(ref burningUntil, duration);

    /// Bullets build toward Soaked: true once `damage` adds up to threshold (then it resets)
    public bool BuildSoak(float damage, float threshold)
    {
        if (!Build(ref soakBuild, ref lastSoakBuild, damage, threshold)) return false;
        Soak();
        return true;
    }

    /// Bullets build toward Charged: true once `damage` adds up to threshold (then it resets)
    public bool BuildCharge(float damage, float threshold)
    {
        if (!Build(ref chargeBuild, ref lastChargeBuild, damage, threshold)) return false;
        Charge();
        return true;
    }

    bool Build(ref float meter, ref float last, float amount, float threshold)
    {
        if (Time.time - last > buildupDecayTime) meter = 0f;
        last = Time.time;
        meter += amount;
        if (meter < threshold) return false;
        meter = 0f;
        return true;
    }

    void Set(ref float until, float duration)
    {
        until = Mathf.Max(until, Time.time + duration);
        ElementStatusFx.On(gameObject);
    }

    public bool Has(ElementStatus status) => status switch
    {
        ElementStatus.Burning => IsBurning,
        ElementStatus.Chilled => IsChilled,
        ElementStatus.Frozen => IsFrozen,
        ElementStatus.Soaked => IsSoaked,
        ElementStatus.Charged => IsCharged,
        ElementStatus.Staggered => IsStaggered,
        ElementStatus.Rooted => IsRooted,
        ElementStatus.Poisoned => IsPoisoned,
        ElementStatus.Marked => VoidMarkedBy != null && echoReady,
        _ => false
    };

    /// A reaction used this status up
    public void Consume(ElementStatus status)
    {
        switch (status)
        {
            case ElementStatus.Burning:
                brands.Clear();
                lastBrandTime.Clear();
                burningUntil = 0f;
                break;
            case ElementStatus.Chilled:
                ClearFreeze();
                break;
            case ElementStatus.Frozen:
                frozenUntil = 0f;
                ClearFreeze();
                EndStun();
                IceEncase.BreakOn(gameObject);
                break;
            case ElementStatus.Soaked: soakedUntil = 0f; soakBuild = 0f; break;
            case ElementStatus.Charged: chargedUntil = 0f; chargeBuild = 0f; break;
            case ElementStatus.Staggered: staggeredUntil = 0f; break;
            case ElementStatus.Rooted: EndStun(); break;
            case ElementStatus.Poisoned: poisonedUntil = 0f; break;
            case ElementStatus.Marked: echoReady = false; break;
        }
    }

    /// Losing a life washes off the element statuses (brands, freeze counters and void
    /// marks keep their own rules)
    public void ClearElementStatuses()
    {
        soakedUntil = chargedUntil = poisonedUntil = staggeredUntil = burningUntil = frozenUntil = 0f;
        soakBuild = chargeBuild = 0f;
        echoReady = false;
        ReactionCooldownUntil = 0f;
    }

    void OnEnable() => DamageEvents.Killed += OnKilled;

    void OnKilled(GameObject victim, GameObject attacker, Vector3 position)
    {
        if (victim == gameObject) ClearElementStatuses();
    }

    void ApplySpeed()
    {
        float freezeMult = 1f - freezeSlowPerStack * freezeStacks;
        if (movement != null)
            movement.SetSpeedModifier("freeze", freezeMult);
        else if (agent != null)
            agent.speed = agentBaseSpeed * freezeMult * stunMultiplier;
    }

    void OnDisable()
    {
        DamageEvents.Killed -= OnKilled;
        // don't leave a player permanently slowed if this component goes away
        if (movement != null) movement.ClearSpeedModifier("freeze");
    }
}
