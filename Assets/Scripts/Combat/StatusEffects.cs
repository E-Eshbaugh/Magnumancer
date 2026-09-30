using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Per-target status effects (brands, freeze counters, stuns, void marks).
/// Added on demand to players and monsters via StatusEffects.Of(target).
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

    /// Can't move for a moment (Thornsnare, Glacial Lance)
    public void Root(float duration) => Stun(0.05f, duration);

    public void ClearFreeze()
    {
        freezeStacks = 0;
        ApplySpeed();
    }

    // ---------- Stun ----------
    /// Slows movement to speedMultiplier for duration (players also get slowed aim via StunEffect).
    public void Stun(float speedMultiplier, float duration)
    {
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
    public void MarkVoid(GameObject caster) => VoidMarkedBy = caster;
    public void ClearVoidMark() => VoidMarkedBy = null;

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
        // don't leave a player permanently slowed if this component goes away
        if (movement != null) movement.ClearSpeedModifier("freeze");
    }
}
