using System.Collections.Generic;
using UnityEngine;

/// Seed of Aloria's healing totem. It's also a nature zone in the Elemental Ecosystem:
/// water surges it (Overgrowth Surge), poison blooms spore pods around it (Blight Bloom),
/// and fire sets it ablaze (Wildfire): no healing while it burns, and the flames hurt
/// everyone near it, its Verdant owner included.
public class HealingZone : MonoBehaviour, IElementZone
{
    [Header("Healing Settings")]
    public float maxHealPerSecond = 10f;
    public HealingBeamController beamController;

    private Dictionary<Transform, float> healAccumulator = new();

    // Overgrowth Surge: water feeds the totem, healing harder for a while
    float surgeMultiplier = 1f, surgeUntil;
    public static readonly List<HealingZone> Active = new();

    public void Surge(float multiplier, float duration)
    {
        surgeMultiplier = multiplier;
        surgeUntil = Time.time + duration;
        Color nature = Elements.ColorOf(Element.Nature);
        AbilityKit.Shockwave(AbilityKit.Ground(transform.position + Vector3.up), 4f, nature, 0.6f);
        PowerFx.Sparks(transform.position + Vector3.up, nature, 30, 4f, 0.8f, 0.08f, -0.3f, Vector3.up, 120f);
    }

    // Wildfire
    float burningUntil;
    bool Burning => Time.time < burningUntil;

    public void Ignite(GameObject by, float duration, float dps)
    {
        burningUntil = Time.time + duration;
        Vector3 at = AbilityKit.Ground(transform.position + Vector3.up);
        var flames = GroundHazard.Spawn(by, at, ZoneRadius, duration, Elements.ColorOf(Element.Fire));
        flames.element = Element.Fire;
        flames.damagePerSecond = dps;
        flames.fromReaction = Reaction.Wildfire;
        EffectPool.Spawn(at, ZoneRadius, duration, EffectPool.Style.Lava);
        PowerFx.Sparks(transform.position + Vector3.up, Elements.ColorOf(Element.Fire), 40, 6f, 0.8f, 0.09f, -0.4f, Vector3.up, 120f);
    }

    void Update()
    {
        if (Burning && Random.value < 0.4f)
            BulletFX.Mote(BulletFX.Flavor.Embers, Elements.ColorOf(Element.Fire),
                          transform.position + Vector3.up * Random.Range(0.3f, 2f) + Random.insideUnitSphere * 0.5f, 1.5f);
    }

    // ---------- IElementZone ----------
    public Element ZoneElement => Burning ? Element.Fire : Element.Nature;
    public GameObject ZoneOwner => null;
    public Vector3 ZoneCenter => transform.position;
    public float ZoneRadius => beamController != null ? beamController.healRange : 3f;
    public float DistanceTo(Vector3 p) => ElementZones.FlatDistance(transform.position, ZoneRadius, p);
    public void Consume() { }   // the totem breaks when its crystal does, not from reactions

    void Start() => ElementZones.Register(this);

    void OnEnable()
    {
        Active.Add(this);
        if (beamController != null)
        {
            Debug.Log("[HealingZone] Subscribed to HealingBeamController");
            beamController.OnHealablePlayersUpdated += HealPlayers;
        }
        else
        {
            Debug.LogError("[HealingZone] beamController reference is missing!");
        }
    }

    void OnDisable()
    {
        Active.Remove(this);
        ElementZones.Unregister(this);
        if (beamController != null)
        {
            beamController.OnHealablePlayersUpdated -= HealPlayers;
        }
    }

    void HealPlayers(List<Transform> players)
    {
        int numTargets = players.Count;
        if (numTargets == 0) return;

        if (Burning) return;   // ablaze: no healing
        float rate = maxHealPerSecond * (Time.time < surgeUntil ? surgeMultiplier : 1f);
        float healRatePerPlayer = rate / numTargets;
        float healThisFrame = healRatePerPlayer * Time.deltaTime;

        foreach (Transform player in players)
        {
            var health = player.GetComponentInParent<PlayerHealthControl>();
            if (health == null || health.currentHealth >= health.maxHealth || health.currentHealth <= 0)
                continue;

            // Initialize accumulator for new players
            if (!healAccumulator.ContainsKey(player))
                healAccumulator[player] = 0f;

            // Accumulate healing over time
            healAccumulator[player] += healThisFrame;

            int healNow = Mathf.FloorToInt(healAccumulator[player]);
            if (healNow > 0)
            {
                health.Heal(healNow);
                healAccumulator[player] -= healNow;

                Debug.Log($"[HealingZone] Healed {player.name} for {healNow} HP (Accumulated: {healAccumulator[player]:F2})");
            }
        }

        // Optional cleanup: remove players who are no longer in the list
        HashSet<Transform> currentSet = new(players);
        List<Transform> toRemove = new();
        foreach (var tracked in healAccumulator.Keys)
        {
            if (!currentSet.Contains(tracked))
                toRemove.Add(tracked);
        }
        foreach (var p in toRemove)
            healAccumulator.Remove(p);
    }
}
