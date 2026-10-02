using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Pack-a-Punch upgrades (pack-a-punch.md). A forged gun gets the baseline (more damage,
/// bigger magazine) plus a power-up chosen by the wizard's ACTIVE rune: forged rounds that
/// land on an enemy are "payloads", and each rune turns payloads into a different combo with
/// that ability. Only a curated set of runes has a power-up so far (see Upgrades); every
/// other build still gets the baseline.
///
/// Rules shared by every power-up: payloads come from real primary-gun hits (Bullet),
/// at most one per owner per PayloadInterval; secondary damage never counts as a payload,
/// so nothing chains into itself, and it pays kill points but not per-hit points.
/// </summary>
public static class ForgedRunes
{
    // ---------- Tuning (provisional, see balance-log.md) ----------

    public const int Price = 2500;
    public const float DamageMultiplier = 1.5f;
    public const float MagazineMultiplier = 1.5f;
    public const float PayloadInterval = 0.75f;
    /// Secondary hits deal this fraction of the triggering forged shot
    public const float SecondaryFraction = 0.5f;
    public const float SecondaryRadius = 2.5f;
    public const int SecondaryTargets = 3;

    // ---------- Catalogue ----------

    public class Upgrade
    {
        public string epithet;
        public string summary;
    }

    /// Power-ups by faction and active rune index (null = baseline only for now)
    static Upgrade[] UpgradesFor(PassiveType faction) => faction switch
    {
        PassiveType.BrandOfFlereous => new[] { null,
            U("Furnace", "Forged hits during Inferno Rounds stretch it: +1s per 3 hits, up to +2s."), null },
        PassiveType.FractalshotShield => new[] {
            U("Rampart", "Forged rounds pass through your own ice walls and throw a frost splinter."), null,
            U("Deepwinter", "Hitting a Flash-Frozen enemy bursts its ice into everyone around it.") },
        PassiveType.LightningReflex => new[] { null,
            U("Conductor", "Forged hits charge a relay: your next Chain Surge jumps once more."), null },
        PassiveType.Undercurrent => new[] { null,
            U("Whirlpool", "Hitting an enemy in your whirlpool splashes everyone trapped with it."), null },
        PassiveType.Stonebind => new[] { null,
            U("Highground", "Firing from your parapet kicks rubble into the enemies around your target."), null },
        PassiveType.VerdantResurgence => new[] {
            U("Sanctuary", "Forged kills inside a Seed totem's circle heal everyone standing in it."), null, null },
        PassiveType.VirulentShroud => new[] { null, null,
            U("Outbreak", "Forged hits on infected enemies leap the infection to a fresh one (3 per cast).") },
        PassiveType.LastRites => new[] { null,
            U("Usurper", "After a Soul Swap, forged hits on that enemy deal +50% for 4s."), null },
        _ => null
    };

    static Upgrade U(string epithet, string summary) => new Upgrade { epithet = epithet, summary = summary };

    public static Upgrade UpgradeFor(WizardData wizard, int activeRune)
    {
        var table = wizard != null ? UpgradesFor(wizard.passive) : null;
        return table != null && activeRune >= 0 && activeRune < table.Length ? table[activeRune] : null;
    }

    // Weapon forms per faction, in the order of Weapons below (pack-a-punch.md)
    static readonly string[] Weapons =
        { "Ironspike", "Ironchant", "Scrapshot", "Blackmaw", "Scrapwind", "Runepiercer",
          "Dustbreaker", "Gravewhisper", "Hellthrasher", "Bloodcurrent", "Iron Tempest", "Emberblast" };

    static string[] FormsFor(PassiveType faction) => faction switch
    {
        PassiveType.BrandOfFlereous => new[] { "Cinder Nail", "Furnace Hymn", "Coalscatter", "Kilnmaw", "Bellowsong", "Pyre Verdict",
            "Hearthbreaker", "Ashen Litany", "Hellforge", "Cinderstream", "Solar Crucible", "Sunspitter" },
        PassiveType.FractalshotShield => new[] { "Hoarfang", "Winter Canticle", "Hailbasket", "Rimejaw", "Sleetwheel", "Absolute Zero",
            "Glacier Knell", "Silent Winter", "Whiteout Engine", "Sleetvein", "Avalanche Choir", "Permafrost Mortar" },
        PassiveType.LightningReflex => new[] { "Stormneedle", "Thunderverse", "Sparkbroom", "Voltmaw", "Dynamo Rattle", "Horizon Bolt",
            "Thunderclap", "Storm Gospel", "Arc Furnace", "Livewire", "Thunderhead", "Stormcell Lobber" },
        PassiveType.Undercurrent => new[] { "Brine Harpoon", "Tide Hymn", "Saltbreaker", "Undertow Jaw", "Foamwheel", "Abyssal Lance",
            "Breakwater", "Drowned Psalm", "Riptide Engine", "Tidal Artery", "Monsoon", "Maelstrom Mortar" },
        PassiveType.Stonebind => new[] { "Bedrock Nail", "Quarry Hymn", "Gravel Choir", "Faultjaw", "Mason's Rattle", "Mountain Verdict",
            "Tectonic Bell", "Lithic Testament", "Quakehammer", "Flintpulse", "Walking Citadel", "Worldbreaker" },
        PassiveType.VerdantResurgence => new[] { "Thornspike", "Grove Hymn", "Briar Basket", "Rootmaw", "Petalwheel", "Heartwood Lance",
            "Bramble Knell", "Green Testament", "Thornthresher", "Sapstream", "Canopy Engine", "Seedfall Mortar" },
        PassiveType.VirulentShroud => new[] { "Plague Needle", "Pestilent Hymn", "Sporebasket", "Rotmaw", "Miasma Rattle", "Patient Zero",
            "Blightbell", "Fever Psalm", "Fumigator", "Venomvein", "Carrion Engine", "Viper Crucible" },
        PassiveType.LastRites => new[] { "Nullspike", "Hollow Hymn", "Graveglass", "Event Maw", "Wraith Rattle", "Final Argument",
            "Death Knell", "Last Testament", "Oblivion Press", "Revenant Vein", "Nightfall Engine", "Eclipse Mortar" },
        _ => null
    };

    /// "Cinderstream · Furnace" (form · active epithet), or just the form without a power-up
    public static string ForgedName(WeaponData gun, WizardData wizard, int activeRune)
    {
        string baseName = gun != null ? gun.weaponName : "Weapon";
        int i = System.Array.IndexOf(Weapons, baseName);
        var forms = wizard != null ? FormsFor(wizard.passive) : null;
        string form = forms != null && i >= 0 ? forms[i] : "Forged " + baseName;
        var up = UpgradeFor(wizard, activeRune);
        return up != null ? $"{form} · {up.epithet}" : form;
    }

    public static WizardData WizardOf(GameObject player)
        => player != null ? player.GetComponentInChildren<WizardAbilityController>()?.wizardData : null;

    public static int ActiveRuneOf(GameObject player)
    {
        var c = player != null ? player.GetComponentInChildren<WizardAbilityController>() : null;
        return c != null ? c.ActiveRune : 0;
    }

    static bool Has(GameObject owner, PassiveType faction, int rune)
    {
        var wiz = WizardOf(owner);
        return wiz != null && wiz.passive == faction && ActiveRuneOf(owner) == rune;
    }

    // ---------- Payload dispatch ----------

    static readonly Dictionary<GameObject, float> nextPayload = new();

    /// True while forge secondary damage is being dealt (no per-hit points for it)
    public static bool DealingSecondary { get; private set; }

    /// A forged primary round hit `victim` for `damage`. Called by Bullet after the hit lands.
    public static void OnForgedHit(GameObject owner, GameObject victim, int damage, Vector3 point, Vector3 dir, bool throughOwnWall)
    {
        if (owner == null || victim == null || victim == owner) return;
        if (!Teams.CanHarm(victim, owner) || Teams.SameTeam(victim, owner)) return;
        if (!DamageEvents.IsCombatant(victim)) return;
        var wiz = WizardOf(owner);
        if (wiz == null) return;
        int rune = ActiveRuneOf(owner);

        // Usurper amplifies every forged hit on the swapped enemy, not just payloads
        if (wiz.passive == PassiveType.LastRites && rune == 1) Usurper(owner, victim, damage);

        float now = Time.time;
        if (nextPayload.TryGetValue(owner, out float ready) && now < ready) return;

        bool fired = wiz.passive switch
        {
            PassiveType.BrandOfFlereous when rune == 1 => Furnace(owner),
            PassiveType.FractalshotShield when rune == 0 => throughOwnWall && Rampart(owner, victim, damage),
            PassiveType.FractalshotShield when rune == 2 => Deepwinter(owner, victim, damage),
            PassiveType.LightningReflex when rune == 1 => Conductor(owner, victim),
            PassiveType.Undercurrent when rune == 1 => Whirlpool(owner, victim, damage),
            PassiveType.Stonebind when rune == 1 => Highground(owner, victim, damage),
            PassiveType.VerdantResurgence when rune == 0 => Sanctuary(owner, victim),
            PassiveType.VirulentShroud when rune == 2 => Outbreak(owner, victim),
            _ => false
        };
        if (fired) nextPayload[owner] = now + PayloadInterval;
    }

    /// Secondary damage: credited to the owner (kills still count) but never a payload
    static void Secondary(GameObject target, float amount, GameObject owner)
    {
        if (!DamageEvents.IsAlive(target)) return;
        DealingSecondary = true;
        try { DamageEvents.Deal(target, amount, owner); }
        finally { DealingSecondary = false; }
    }

    /// Up to SecondaryTargets living enemies near `center`, nearest first, skipping `except`
    static List<GameObject> Around(Vector3 center, float radius, GameObject owner, GameObject except, int max = SecondaryTargets)
    {
        var list = AbilityKit.Enemies(center, radius, owner);
        list.Remove(except);
        list.Sort((a, b) => (a.transform.position - center).sqrMagnitude.CompareTo((b.transform.position - center).sqrMagnitude));
        if (list.Count > max) list.RemoveRange(max, list.Count - max);
        return list;
    }

    // ---------- Emberguard II · Furnace ----------

    static bool Furnace(GameObject owner)
    {
        var buff = owner.GetComponent<InfernoBuff>();
        if (buff == null || !buff.Active) return false;
        if (buff.ForgeHit(3, 1f, 2f))
        {
            Color fire = Elements.ColorOf(Element.Fire);
            PowerFx.Sparks(AbilityKit.Chest(owner), fire, 14, 4f, 0.4f, 0.07f, 1.5f);
            PowerFx.Flash(AbilityKit.Chest(owner), fire, 4f, 4f, 0.25f);
        }
        return true;
    }

    // ---------- Frostwarden I · Rampart ----------

    /// A forged round from `owner` may fly through this collider (their own ice wall)
    public static bool PassesOwnWall(GameObject owner, Collider col)
    {
        if (owner == null || col == null) return false;
        var wall = col.GetComponentInParent<IceWallEffect>();
        return wall != null && wall.owner == owner && Has(owner, PassiveType.FractalshotShield, 0);
    }

    static bool Rampart(GameObject owner, GameObject victim, int damage)
    {
        Color frost = Elements.ColorOf(Element.Frost);
        Vector3 from = AbilityKit.Chest(victim);
        var targets = Around(victim.transform.position, SecondaryRadius, owner, victim, 1);
        StatusEffects.Of(victim).AddFreeze();
        PowerFx.IceShards(from, frost, 8, 5f, 0.14f);
        foreach (var t in targets)
        {
            AbilityKit.Zap(from, AbilityKit.Chest(t), frost, 0.15f, 0.1f);
            PowerFx.IceShards(AbilityKit.Chest(t), frost, 6, 4f, 0.12f);
            Secondary(t, damage * SecondaryFraction, owner);
            if (DamageEvents.IsAlive(t)) StatusEffects.Of(t).AddFreeze();
        }
        return true;
    }

    // ---------- Frostwarden III · Deepwinter ----------

    static bool Deepwinter(GameObject owner, GameObject victim, int damage)
    {
        if (!IceEncase.ClaimForgeBurst(victim)) return false;
        Color frost = Elements.ColorOf(Element.Frost);
        Vector3 at = AbilityKit.Chest(victim);
        float burst = Mathf.Max(30f, damage);   // a Shatter's worth, or the shot if bigger
        PowerFx.IceShards(at, frost, 26, 9f, 0.24f);
        PowerFx.Sparks(at, Color.white, 20, 7f, 0.4f, 0.06f, 1f);
        AbilityKit.Shockwave(victim.transform.position, SecondaryRadius, frost, 0.35f);
        CameraShake.Shake(0.12f, 0.12f);
        foreach (var t in Around(victim.transform.position, SecondaryRadius, owner, victim))
        {
            AbilityKit.Zap(at, AbilityKit.Chest(t), frost, 0.18f, 0.14f);
            Secondary(t, burst, owner);
        }
        return true;
    }

    // ---------- Voltborn II · Conductor ----------

    class Relay { public GameObject target; public float until; public LineRenderer ring; }
    static readonly Dictionary<GameObject, Relay> relays = new();
    const float RelayTime = 4f;

    static bool Conductor(GameObject owner, GameObject victim)
    {
        if (!relays.TryGetValue(owner, out var r)) relays[owner] = r = new Relay();
        r.target = victim;
        r.until = Time.time + RelayTime;
        Color bolt = Elements.ColorOf(Element.Lightning);
        PowerFx.Sparks(AbilityKit.Chest(victim), bolt, 10, 4f, 0.3f, 0.05f, 0.5f);
        RelayMarker.Show(victim, bolt, RelayTime);
        return true;
    }

    /// Chain Surge asks for its bonus jump: 1 while the caster has a living relay (used up)
    public static int ConsumeRelayJumps(GameObject caster)
    {
        if (caster == null || !relays.TryGetValue(caster, out var r)) return 0;
        bool live = r.target != null && Time.time < r.until && DamageEvents.IsAlive(r.target);
        if (r.target != null) RelayMarker.Clear(r.target);
        relays.Remove(caster);
        return live ? 1 : 0;
    }

    // ---------- Tidebound II · Whirlpool ----------

    static bool Whirlpool(GameObject owner, GameObject victim, int damage)
    {
        GroundHazard pool = null;
        foreach (var h in Object.FindObjectsByType<GroundHazard>(FindObjectsSortMode.None))
            if (h.owner == owner && h.pullStrength > 0f && FlatDistance(h.transform.position, victim.transform.position) <= h.radius)
            { pool = h; break; }
        if (pool == null) return false;

        Color water = Elements.ColorOf(Element.Water);
        Vector3 from = AbilityKit.Chest(victim);
        PowerFx.Puffs(from, water, 5, 2f, 0.4f, 0.4f, additive: true);
        foreach (var t in Around(pool.transform.position, pool.radius, owner, victim))
        {
            AbilityKit.Zap(from, AbilityKit.Chest(t), water, 0.18f, 0.12f);
            PowerFx.Puffs(AbilityKit.Chest(t), water, 3, 1.5f, 0.3f, 0.35f, additive: true);
            Secondary(t, damage * SecondaryFraction, owner);
        }
        return true;
    }

    static float FlatDistance(Vector3 a, Vector3 b) { a.y = b.y = 0f; return Vector3.Distance(a, b); }

    // ---------- Granite Vow II · Highground ----------

    static bool Highground(GameObject owner, GameObject victim, int damage)
    {
        var parapet = owner.GetComponentInChildren<EarthworkParapetAbility>();
        var move = owner.GetComponent<PlayerMovement3D>();
        if (parapet == null || !parapet.TowerUp || move == null || move.elevation < 0.5f) return false;

        Color earth = Elements.ColorOf(Element.Earth);
        Vector3 at = victim.transform.position;
        RockDebris.Burst(at, 6, 5f, 0.25f);
        AbilityKit.Shockwave(at, SecondaryRadius, earth, 0.3f);
        foreach (var t in Around(at, SecondaryRadius, owner, victim))
        {
            RockDebris.Chunk(AbilityKit.Chest(t), Vector3.up * 3f, 0.2f, 0.6f);
            Secondary(t, damage * SecondaryFraction, owner);
        }
        return true;
    }

    // ---------- Verdant Circle I · Sanctuary ----------

    const int SanctuaryHeal = 5;

    static bool Sanctuary(GameObject owner, GameObject victim)
    {
        if (DamageEvents.IsAlive(victim)) return false;   // kills only
        Vector3 at = victim.transform.position;
        foreach (var zone in HealingZone.Active)
        {
            if (zone == null || zone.DistanceTo(at) > 0f) continue;
            Color green = Elements.ColorOf(Element.Nature);
            AbilityKit.Shockwave(zone.ZoneCenter, zone.ZoneRadius, green, 0.45f);
            PowerFx.Sparks(at + Vector3.up, green, 12, 3f, 0.5f, 0.06f, -0.5f);
            foreach (var p in PlayerHealthControl.ActivePlayers)
                if (p != null && p.IsStanding && zone.DistanceTo(p.transform.position) <= 0f
                    && (p.gameObject == owner || Teams.SameTeam(p.gameObject, owner)))
                {
                    p.Heal(SanctuaryHeal);
                    PowerFx.Sparks(AbilityKit.Chest(p.gameObject), green, 8, 2f, 0.4f, 0.05f, -1f);
                }
            return true;
        }
        return false;
    }

    // ---------- Blightward III · Outbreak ----------

    const int OutbreaksPerCast = 3;
    const float OutbreakRange = 6f;
    static readonly Dictionary<GameObject, int> outbreaksLeft = new();

    /// Contagion was cast: refill this caster's spreads
    public static void ContagionCast(GameObject caster)
    {
        if (caster != null) outbreaksLeft[caster] = OutbreaksPerCast;
    }

    static bool Outbreak(GameObject owner, GameObject victim)
    {
        var inf = victim.GetComponent<Infection>();
        if (inf == null || inf.Source != owner || inf.forgeSpread) return false;
        if (!outbreaksLeft.TryGetValue(owner, out int left) || left <= 0) return false;

        GameObject next = null;
        foreach (var e in Around(victim.transform.position, OutbreakRange, owner, victim, 8))
            if (e.GetComponent<Infection>() == null) { next = e; break; }
        if (next == null) return false;

        inf.forgeSpread = true;
        outbreaksLeft[owner] = left - 1;
        Infection.Apply(next, owner, inf.Color, inf.Remaining);
        Color c = inf.Color;
        AbilityKit.Zap(AbilityKit.Chest(victim), AbilityKit.Chest(next), c, 0.3f, 0.18f);
        PowerFx.Puffs(AbilityKit.Chest(next), c, 6, 1.5f, 0.5f, 0.6f);
        return true;
    }

    // ---------- The Hollow II · Usurper ----------

    class Swap { public GameObject target; public float until; }
    static readonly Dictionary<GameObject, Swap> swaps = new();
    const float UsurpTime = 4f;
    const float UsurpBonus = 0.5f;

    /// Soul Swap succeeded against target
    public static void SoulSwapped(GameObject caster, GameObject target)
    {
        if (caster == null || target == null) return;
        swaps[caster] = new Swap { target = target, until = Time.time + UsurpTime };
    }

    static void Usurper(GameObject owner, GameObject victim, int damage)
    {
        if (!swaps.TryGetValue(owner, out var s) || s.target != victim || Time.time >= s.until) return;
        Color dark = Elements.ColorOf(Element.Void);
        PowerFx.Sparks(AbilityKit.Chest(victim), dark, 5, 3f, 0.3f, 0.05f, 0.5f);
        Secondary(victim, damage * UsurpBonus, owner);
    }

    // ---------- Reset ----------

    static void Clear()
    {
        nextPayload.Clear();
        relays.Clear();
        outbreaksLeft.Clear();
        swaps.Clear();
        DealingSecondary = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Clear();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) Clear();
    }
}

/// Crackling ring on a Conductor relay target
public class RelayMarker : MonoBehaviour
{
    LineRenderer ring;
    float until;
    Color color;

    public static void Show(GameObject target, Color color, float time)
    {
        var m = target.GetComponent<RelayMarker>();
        if (m == null) m = target.AddComponent<RelayMarker>();
        m.color = color;
        m.until = Time.time + time;
    }

    public static void Clear(GameObject target)
    {
        var m = target != null ? target.GetComponent<RelayMarker>() : null;
        if (m != null) m.until = 0f;
    }

    void Start()
    {
        ring = GlowLine.Make(transform, "RelayRing", 20, 0.07f, AbilityKit.Glow());
        ring.loop = true;
    }

    void Update()
    {
        if (Time.time >= until || !DamageEvents.IsAlive(gameObject))
        {
            if (ring != null) Destroy(ring.gameObject);
            Destroy(this);
            return;
        }
        AbilityKit.Circle(ring, transform.position + Vector3.up * 1.9f, 0.35f + 0.05f * Mathf.Sin(Time.time * 20f));
        GlowLine.SetColor(ring, color, 0.9f);
    }
}
