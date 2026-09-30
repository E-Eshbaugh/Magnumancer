using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum Reaction { Shatter, Conduct, Combust, ThermalShock, Brittle, Mudslide, Wildfire, Steam, Echo }

/// <summary>
/// The Elemental Ecosystem's reactions: an element hitting a target that carries another
/// element's status sets off a reaction. Reactions go off no matter who applied the status
/// (free-for-all included), consume it, and credit their damage to whoever triggered them
/// (through DamageEvents, so kills, passives and rumble all see it). At most one reaction
/// per hit, then a short per-target cooldown. A Void-marked target Echoes the reaction.
///
/// Sources call in here: bullets (BulletHit), abilities (AbilityHit), ground zones
/// (ZoneHit), plus OnElementPass / OnElementArea for bullets and blasts meeting zones.
/// Every recipe and number is in the tables below. See Docs/Design/elemental-ecosystem.md.
/// </summary>
public static class ElementReactions
{
    // ---------- Tuning ----------

    /// Recipes, most important first (a fire hit on someone Soaked *and* Poisoned Combusts)
    static readonly Recipe[] Table =
    {
        new Recipe
        {
            id = Reaction.Shatter, word = "SHATTER", colorA = Element.Frost, colorB = Element.Earth,
            pairs = new[] { (Element.Earth, ElementStatus.Frozen) },
            heavyTriggers = true,                 // any heavy knockback (sniper round, Winter's Wind)
            damage = 10f, damagePerCounter = 4f,  // frozen counts as 5 counters: 30
            knockback = 14f, shake = 0.22f,
            effect = Shatter,
        },
        new Recipe
        {
            id = Reaction.Conduct, word = "CONDUCT", colorA = Element.Water, colorB = Element.Lightning,
            pairs = new[] { (Element.Lightning, ElementStatus.Soaked), (Element.Water, ElementStatus.Charged) },
            damage = 16f,      // to each Soaked target
            radius = 8f,       // jumps to every Soaked target this close
            stun = 0.6f,
            duration = 3f,     // electrified water zones
            zoneDps = 10f,     // shocks while standing in them
            shake = 0.18f,
            effect = Conduct,
        },
        new Recipe
        {
            id = Reaction.Combust, word = "COMBUST", colorA = Element.Poison, colorB = Element.Fire,
            pairs = new[] { (Element.Fire, ElementStatus.Poisoned), (Element.Poison, ElementStatus.Burning) },
            damage = 26f,      // center of the blast, 40% at the edge
            radius = 3.5f,     // around a Poisoned target; a cloud blasts its own size + 1.5
            knockback = 16f,
            duration = 3f,     // burning patch left behind
            zoneDps = 8f,
            shake = 0.35f,
            effect = Combust,
        },
        new Recipe
        {
            id = Reaction.ThermalShock, word = "THERMAL SHOCK", colorA = Element.Frost, colorB = Element.Fire,
            pairs = new[] { (Element.Fire, ElementStatus.Frozen), (Element.Fire, ElementStatus.Chilled), (Element.Frost, ElementStatus.Burning) },
            minCounters = 2,                      // a single stray counter isn't worth cashing
            damage = 4f, damagePerCounter = 6f,   // 16 at 2 counters, 34 frozen
            shake = 0.25f,
            effect = ThermalShock,
        },
        new Recipe
        {
            id = Reaction.Brittle, word = "BRITTLE", colorA = Element.Water, colorB = Element.Frost,
            pairs = new[] { (Element.Frost, ElementStatus.Soaked), (Element.Water, ElementStatus.Chilled) },
            damage = 8f,
            freezeTo = 4,      // straight to 4 counters: one more frost hit freezes
            radius = 4f,       // water zones this close freeze over
            duration = 4f,     // ...into ice for this long
            traction = 0.12f,  // how slippery the ice is (1 = normal)
            shake = 0.12f,
            effect = Brittle,
        },
        new Recipe
        {
            id = Reaction.Mudslide, word = "MUDSLIDE", colorA = Element.Water, colorB = Element.Earth,
            pairs = new[] { (Element.Earth, ElementStatus.Soaked), (Element.Water, ElementStatus.Staggered) },
            damage = 6f,
            radius = 2.8f,     // mud patch
            duration = 4f,
            slow = 0.45f,      // 55% slower in the mud, and no dashing out
            shake = 0.15f,
            effect = Mudslide,
        },
        new Recipe
        {
            id = Reaction.Wildfire, word = "WILDFIRE", colorA = Element.Nature, colorB = Element.Fire,
            pairs = new[] { (Element.Fire, ElementStatus.Rooted), (Element.Nature, ElementStatus.Burning) },
            damage = 12f,
            radius = 2.5f,     // burn zone, which spreads into any growth it touches
            duration = 4f,
            zoneDps = 8f,
            shake = 0.2f,
            effect = Wildfire,
        },
        new Recipe
        {
            id = Reaction.Steam, word = "STEAM", colorA = Element.Water, colorB = Element.Fire,
            pairs = new[] { (Element.Fire, ElementStatus.Soaked), (Element.Water, ElementStatus.Burning) },
            damage = 10f,      // scald on the target
            radius = 3.2f,     // cloud that hides whoever's inside
            duration = 3f,
            zoneDps = 4f,      // scalding while inside
            shake = 0.1f,
            effect = Steam,
        },
    };

    /// Element hits (bullets flying through, blasts, other zones) meeting zones on the
    /// ground: (the hit, the zone it meets, what happens to the zone)
    static readonly (Element hit, Element zone, Reaction reaction)[] ZoneRules =
    {
        (Element.Fire, Element.Poison, Reaction.Combust),     // the gas explodes
        (Element.Fire, Element.Nature, Reaction.Wildfire),    // the growth catches fire
        (Element.Lightning, Element.Water, Reaction.Conduct), // the water is electrified
        (Element.Frost, Element.Water, Reaction.Brittle),     // the water freezes into ice
        (Element.Earth, Element.Water, Reaction.Mudslide),    // the water turns to mud
    };

    /// Reactions can't go off on the same target again for this long
    public static float ReactionCooldown = 1f;
    /// A single bullet this strong counts as heavy knockback (M1 30, SVD 45)
    public static float HeavyBulletDamage = 30f;
    /// Bullet damage that adds up to Soaked / Charged (Tidebound / Voltborn bullets)
    public static float SoakBuildup = 35f;
    public static float ChargeBuildup = 30f;
    /// How long fire abilities and fire zones leave someone Burning
    public static float AbilityBurnTime = 3f;
    public static float ZoneBurnTime = 1.5f;
    /// Echo (Void mark): the reaction repeats on the marked target this much later, this much harder
    public static float EchoDelay = 0.35f;
    public static float EchoScale = 1.5f;
    /// Reactions setting off reactions (Combust patch → another cloud) stop this deep
    const int MaxDepth = 4;

    public class Recipe
    {
        public Reaction id;
        public string word;
        public Element colorA, colorB;
        public (Element trigger, ElementStatus status)[] pairs;
        public bool heavyTriggers;
        public int minCounters, freezeTo;
        public float damage, damagePerCounter, radius, stun, knockback, duration, zoneDps, slow = 1f, traction = 1f, shake;
        public Action<Recipe, Ctx> effect;
    }

    /// One reaction going off
    public class Ctx
    {
        public GameObject target;        // null when a zone reacted (bullet through a cloud)
        public GameObject attacker;      // credit
        public Element element;          // what set it off
        public ElementStatus consumed;
        public IElementZone zone;        // the zone that reacted, if any
        public Vector3 point;
        public int counters;             // freeze counters when it went off
        public int count = 1;            // targets caught, shown as "x3"
        public float scale = 1f;         // damage multiplier (Echo)
        public bool echo;
    }

    /// reaction (Echo for the repeat), triggerer (credit), target (null for zone
    /// reactions), where. Hook for the combo counter, announcer and kill feed.
    public static event Action<Reaction, GameObject, GameObject, Vector3> Reacted;

    // ---------- Sources ----------

    /// A bullet landed: react, or build the shooter's element status (Tidebound bullets
    /// build Soaked, Voltborn bullets build Charged, heavy Granite Vow rounds Stagger).
    public static bool BulletHit(GameObject target, GameObject shooter, float damage, Vector3 point)
    {
        var element = Elements.Of(shooter);
        bool heavy = damage >= HeavyBulletDamage;
        if (OnElementHit(target, shooter, element, damage, point, heavy)) return true;
        if (!IsTarget(target, shooter)) return false;

        var fx = StatusEffects.Of(target);
        switch (element)
        {
            case Element.Water: fx.BuildSoak(damage, SoakBuildup); break;
            case Element.Lightning: fx.BuildCharge(damage, ChargeBuildup); break;
            case Element.Earth: if (heavy) fx.Stagger(); break;
        }
        return false;
    }

    /// An ability of the caster's element hit target: react, or leave the full status.
    public static bool AbilityHit(GameObject target, GameObject caster, float damage, bool heavy = false)
        => ElementHit(target, caster, Elements.Of(caster), damage, heavy, AbilityBurnTime);

    /// Same, with an explicit element (Combust's burning patch is fire whoever made it)
    public static bool AbilityHit(GameObject target, GameObject caster, Element element, float damage, bool heavy = false)
        => ElementHit(target, caster, element, damage, heavy, AbilityBurnTime);

    /// A zone ticked on someone standing in it (lava, poison cloud, whirlpool, static field)
    public static bool ZoneHit(GameObject target, GameObject owner, Element element, float damage)
        => ElementHit(target, owner, element, damage, false, ZoneBurnTime);

    static bool ElementHit(GameObject target, GameObject attacker, Element element, float damage, bool heavy, float burnTime)
    {
        if (target == null) return false;
        if (OnElementHit(target, attacker, element, damage, AbilityKit.Chest(target), heavy)) return true;
        if (!IsTarget(target, attacker)) return false;

        var fx = StatusEffects.Of(target);
        switch (element)
        {
            case Element.Fire: fx.SetBurning(burnTime); break;
            case Element.Water: fx.Soak(); break;
            case Element.Lightning: fx.Charge(); break;
            case Element.Poison: fx.Poison(); break;
            case Element.Earth: fx.Stagger(); break;
            // Frost, Nature and Void abilities apply their own statuses (freeze, root, mark)
        }
        if (heavy) fx.Stagger();
        return false;
    }

    /// Checks target's statuses against `element` and sets off at most one reaction.
    /// True if one went off (its status is consumed; callers skip applying their own).
    public static bool OnElementHit(GameObject target, GameObject attacker, Element element, float damage, Vector3 point, bool heavy = false)
    {
        if (depth >= MaxDepth || !IsTarget(target, attacker)) return false;
        if (element == Element.None && !heavy) return false;

        var fx = StatusEffects.Of(target);
        if (Time.time < fx.ReactionCooldownUntil) return false;

        foreach (var r in Table)
            foreach (var (trigger, status) in r.pairs)
            {
                bool triggered = (element != Element.None && trigger == element) || (heavy && r.heavyTriggers);
                if (!triggered || !fx.Has(status)) continue;
                if (r.minCounters > 0 && !fx.IsFrozen && fx.FreezeStacks < r.minCounters) continue;

                var c = new Ctx
                {
                    target = target, attacker = attacker, element = element, consumed = status,
                    point = point, counters = fx.IsFrozen ? 5 : fx.FreezeStacks,
                };
                fx.Consume(status);
                fx.ReactionCooldownUntil = Time.time + ReactionCooldown;
                Fire(r, c);
                return true;
            }
        return false;
    }

    /// A bullet's step from→to: a round flying through a zone its element reacts with
    /// (fire through gas or brambles, frost or lightning through water). One per bullet.
    public static bool OnElementPass(Vector3 from, Vector3 to, GameObject attacker, Element element)
    {
        if (element == Element.None || depth >= MaxDepth) return false;
        foreach (var rule in ZoneRules)
        {
            if (rule.hit != element) continue;
            var zone = ElementZones.AlongSegment(from, to, rule.zone);
            if (zone == null) continue;
            ZoneReaction(rule.reaction, zone, attacker, element);
            return true;
        }
        return false;
    }

    /// A burst of `element` over an area (fireball, brand ignite, Blinkstorm pulse, Flash
    /// Freeze, Seismic Judgement) meets the zones there.
    public static void OnElementArea(Vector3 center, float radius, GameObject attacker, Element element)
    {
        if (depth >= MaxDepth) return;
        foreach (var rule in ZoneRules)
            if (rule.hit == element)
                foreach (var zone in ElementZones.Overlapping(center, radius, rule.zone))
                    ZoneReaction(rule.reaction, zone, attacker, element);
    }

    /// A zone appeared: it reacts with zones it touches (a mine's cloud on lava, lava laid
    /// through a cloud, a static field on a whirlpool, brambles grown into a fire).
    /// The newcomer gets the credit.
    internal static void OnZoneSpawned(IElementZone zone)
    {
        if (!Array.Exists(ZoneRules, r => r.hit == zone.ZoneElement || r.zone == zone.ZoneElement)) return;
        // a beat later, so chains ripple outward like Explosions do
        Runner().After(0.15f, () => ZoneMeetsZones(zone));
    }

    /// A zone changed element (water froze, brambles caught fire): react like a new one
    public static void OnZoneChanged(IElementZone zone) => OnZoneSpawned(zone);

    static void ZoneMeetsZones(IElementZone zone)
    {
        if (zone is UnityEngine.Object o && o == null) return;
        Element e = zone.ZoneElement;
        foreach (var rule in ZoneRules)
        {
            // the newcomer acts on zones it touches...
            if (rule.hit == e)
                foreach (var other in ElementZones.Overlapping(zone.ZoneCenter, zone.ZoneRadius + 10f, rule.zone, zone))
                    if (ElementZones.Touch(zone, other)) ZoneReaction(rule.reaction, other, zone.ZoneOwner, e);

            // ...or gets acted on by one it landed in
            if (rule.zone == e)
                foreach (var other in ElementZones.Overlapping(zone.ZoneCenter, zone.ZoneRadius + 10f, rule.hit, zone))
                    if (ElementZones.Touch(zone, other))
                    {
                        ZoneReaction(rule.reaction, zone, zone.ZoneOwner, rule.hit);
                        return;
                    }
        }
    }

    static void ZoneReaction(Reaction id, IElementZone zone, GameObject attacker, Element element)
    {
        var r = Array.Find(Table, x => x.id == id);
        Fire(r, new Ctx { attacker = attacker, element = element, zone = zone, point = zone.ZoneCenter + Vector3.up });
    }

    // ---------- Running a reaction ----------

    static int depth;

    static void Fire(Recipe r, Ctx c)
    {
        depth++;
        try { r.effect(r, c); }
        finally { depth--; }
        Feedback(r, c);
        Reacted?.Invoke(c.echo ? Reaction.Echo : r.id, c.attacker, c.target, c.point);
        TryEcho(r, c);
    }

    /// Void-marked target: the reaction goes off again on them, harder
    static void TryEcho(Recipe r, Ctx c)
    {
        if (c.echo || c.target == null) return;
        var fx = StatusEffects.Of(c.target);
        if (!fx.Has(ElementStatus.Marked)) return;
        fx.Consume(ElementStatus.Marked);
        var hollow = fx.VoidMarkedBy;
        var target = c.target;

        // the mark drinks the reaction in...
        Color voidColor = Elements.ColorOf(Element.Void);
        PowerFx.Puffs(AbilityKit.Chest(target), voidColor, 8, 1.5f, 0.7f, EchoDelay, additive: true, lift: 0f);

        // ...and throws it back out
        Runner().After(EchoDelay, () =>
        {
            if (!IsTarget(target, c.attacker)) return;
            Fire(r, new Ctx
            {
                target = target, attacker = c.attacker, element = c.element, consumed = c.consumed,
                point = AbilityKit.Chest(target), counters = c.counters, scale = EchoScale, echo = true,
            });
            AbilityKit.Shockwave(AbilityKit.Ground(target.transform.position + Vector3.up), 3f, voidColor, 0.4f);
            if (hollow != null && hollow != c.attacker) Rumble.ReactionTrigger(hollow); // The Hollow feels their mark pay off
        });
    }

    static void Feedback(Recipe r, Ctx c)
    {
        Color a = Elements.ColorOf(c.echo ? Element.Void : r.colorA), b = Elements.ColorOf(r.colorB);
        string word = (c.echo ? "ECHO " : "") + r.word + "!" + (c.count > 1 ? $" <size=70%>x{c.count}</size>" : "");
        ReactionPopup.Show(word, a, b, c.point + Vector3.up * 1.2f, c.echo ? 1.15f : 1f);

        Vector3 ground = AbilityKit.Ground(c.point);
        AbilityKit.Shockwave(ground, 2.2f, a, 0.35f);
        AbilityKit.Shockwave(ground, 1.4f, b, 0.28f);
        PowerFx.Flash(c.point, Color.Lerp(a, b, 0.5f), 8f, 7f, 0.35f);

        CameraShake.Shake(r.shake * c.scale, 0.2f);
        Rumble.ReactionTrigger(c.attacker);
    }

    /// Reaction damage: credited to the triggerer, never to themselves
    static void Hurt(GameObject victim, float damage, Ctx c)
    {
        if (victim == null || victim == c.attacker) return;
        DamageEvents.Deal(victim, damage * c.scale, c.attacker);
        Rumble.ReactionVictim(victim);
    }

    static bool IsTarget(GameObject target, GameObject attacker)
    {
        if (target == null || target == attacker || !target.activeInHierarchy) return false;
        if (!DamageEvents.IsCombatant(target)) return false;
        var h = target.GetComponent<PlayerHealthControl>();
        return h == null || !h.IsDead;
    }

    /// Shove away from whoever set it off
    static Vector3 AwayFrom(GameObject t, Ctx c)
    {
        Vector3 from = c.attacker != null ? c.attacker.transform.position : c.point;
        Vector3 away = t.transform.position - from; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = UnityEngine.Random.insideUnitSphere;
        away.y = 0f;
        return away.normalized;
    }

    // ---------- Shatter: Frozen + Earth / heavy knockback ----------

    static void Shatter(Recipe r, Ctx c)
    {
        var t = c.target;
        if (t == null) return;
        Color frost = Elements.ColorOf(Element.Frost), earth = Elements.ColorOf(Element.Earth);
        Vector3 chest = AbilityKit.Chest(t);

        Hurt(t, r.damage + r.damagePerCounter * c.counters, c);
        AbilityKit.Knockback(t, AwayFrom(t, c) * r.knockback);   // knocked clean out of the ice

        PowerFx.IceShards(chest, frost, 34, 10f, 0.28f);
        PowerFx.Sparks(chest, Color.white, 30, 9f, 0.45f, 0.07f, 1f);
        PowerFx.Sparks(chest, earth, 14, 6f, 0.5f, 0.1f, 2f);
        PowerFx.Puffs(chest, new Color(0.85f, 0.95f, 1f, 1f), 8, 3f, 0.8f, 0.6f, additive: true);
        for (int i = 0; i < 10; i++)
            BulletFX.Mote(BulletFX.Flavor.Frost, frost, chest + UnityEngine.Random.insideUnitSphere * 0.6f, 2f);
    }

    // ---------- Conduct: Water + Lightning ----------

    static void Conduct(Recipe r, Ctx c)
    {
        // Lightning met a water zone (bullet, Blinkstorm pulse, static field): electrify it
        if (c.target == null)
        {
            if (c.zone is IElectrifiable z) z.Electrify(c.attacker, r.duration, r.zoneDps * c.scale);
            return;
        }

        Vector3 at = c.target.transform.position;
        var chain = new List<GameObject> { c.target };
        foreach (var e in AbilityKit.Enemies(at, r.radius, c.attacker))
        {
            if (e == c.target) continue;
            var fx = StatusEffects.Of(e);
            if (!fx.IsSoaked) continue;
            fx.Consume(ElementStatus.Soaked);
            fx.ReactionCooldownUntil = Time.time + ReactionCooldown;
            chain.Add(e);
        }
        c.count = chain.Count;
        Runner().StartCoroutine(ConductChain(r, c, Order(chain)));

        // standing water nearby carries the charge
        foreach (var zone in ElementZones.Overlapping(at, r.radius, Element.Water))
            if (zone is IElectrifiable z)
            {
                z.Electrify(c.attacker, r.duration, r.zoneDps * c.scale);
                DualZap(AbilityKit.Chest(c.target), zone.ZoneCenter + Vector3.up * 0.3f, 0.3f);
            }
    }

    /// Nearest-neighbor path so the bolt visibly hops target to target
    static List<GameObject> Order(List<GameObject> targets)
    {
        var path = new List<GameObject> { targets[0] };
        var left = new List<GameObject>(targets.GetRange(1, targets.Count - 1));
        while (left.Count > 0)
        {
            Vector3 from = path[path.Count - 1].transform.position;
            left.Sort((x, y) => (x.transform.position - from).sqrMagnitude.CompareTo((y.transform.position - from).sqrMagnitude));
            path.Add(left[0]);
            left.RemoveAt(0);
        }
        return path;
    }

    static IEnumerator ConductChain(Recipe r, Ctx c, List<GameObject> chain)
    {
        Color volt = Elements.ColorOf(Element.Lightning);
        Vector3 prev = c.point;
        for (int i = 0; i < chain.Count; i++)
        {
            var t = chain[i];
            if (t == null || !t.activeInHierarchy) continue;
            Vector3 chest = AbilityKit.Chest(t);
            if (i > 0) DualZap(prev, chest, 0.3f);

            Hurt(t, r.damage, c);
            StatusEffects.Of(t).StunAtLeast(0.25f, r.stun);
            PowerFx.Sparks(chest, volt, 24, 7f, 0.35f, 0.06f, 0.6f);
            PowerFx.Flash(chest, volt, 5f, 4f, 0.2f);
            for (int k = 0; k < 3; k++)
                AbilityKit.Zap(chest, chest + UnityEngine.Random.onUnitSphere * 1.1f, volt, 0.15f, 0.08f);

            prev = chest;
            yield return new WaitForSeconds(0.06f);
        }
    }

    /// A thick water-colored bolt with a bright lightning core
    static void DualZap(Vector3 a, Vector3 b, float time)
    {
        AbilityKit.Zap(a, b, Elements.ColorOf(Element.Water), time, 0.34f);
        AbilityKit.Zap(a, b, Elements.ColorOf(Element.Lightning), time, 0.14f);
    }

    // ---------- Combust: Fire + Poison ----------

    static void Combust(Recipe r, Ctx c)
    {
        // the cloud they're standing in goes up with them
        var cloud = c.zone;
        if (cloud == null && c.target != null)
        {
            var clouds = ElementZones.At(c.target.transform.position, Element.Poison);
            if (clouds.Count > 0) cloud = clouds[0];
        }
        if (cloud == null && c.target == null) return;

        Vector3 center = cloud != null ? cloud.ZoneCenter : c.target.transform.position;
        center = AbilityKit.Ground(center + Vector3.up);
        float radius = cloud != null ? Mathf.Max(r.radius, cloud.ZoneRadius + 1.5f) : r.radius;
        cloud?.Consume();
        c.point = center + Vector3.up;

        // sets off mines and grenades too: chain reactions
        Explosions.AffectWorld(center, radius, r.damage, null);
        Rumble.Blast(center, radius * 2f);

        foreach (var e in AbilityKit.Enemies(center, radius, null))
        {
            Vector3 d = e.transform.position - center; d.y = 0f;
            float k = 1f - Mathf.Clamp01(d.magnitude / radius);
            if (d.sqrMagnitude < 0.01f) d = UnityEngine.Random.insideUnitSphere;
            d.y = 0f;
            AbilityKit.Knockback(e, d.normalized * r.knockback * Mathf.Lerp(0.5f, 1f, k));
            if (e == c.attacker) continue;   // shoved, never hurt, by your own reaction

            var fx = StatusEffects.Of(e);
            fx.Consume(ElementStatus.Poisoned);  // one blast per cloud, not one per victim
            fx.ReactionCooldownUntil = Time.time + ReactionCooldown;
            Hurt(e, r.damage * Mathf.Lerp(0.4f, 1f, k), c);
            fx.SetBurning(AbilityBurnTime);
        }

        // a burning patch where the gas was
        var patch = GroundHazard.Spawn(c.attacker, center, radius * 0.6f, r.duration, Elements.ColorOf(Element.Fire));
        patch.element = Element.Fire;
        patch.damagePerSecond = r.zoneDps;
        EffectPool.Spawn(center, radius * 0.6f, r.duration, EffectPool.Style.Lava);

        Color fire = Elements.ColorOf(Element.Fire), poison = Elements.ColorOf(Element.Poison);
        Vector3 up = center + Vector3.up * 0.8f;
        AbilityKit.Shockwave(center, radius * 1.2f, fire, 0.45f);
        AbilityKit.Shockwave(center, radius * 0.8f, poison, 0.35f);
        PowerFx.Flash(up, fire, 14f, radius * 3f, 0.5f);
        PowerFx.Sparks(up, fire, 60, 11f, 0.9f, 0.1f, 1.2f, Vector3.up, 150f);
        PowerFx.Puffs(up, Color.Lerp(fire, Color.white, 0.3f), 14, 4f, 1.4f, 0.7f, additive: true, lift: 2f);
        PowerFx.Puffs(up, new Color(0.2f, 0.28f, 0.08f, 1f), 12, 3f, 1.6f, 1.6f, lift: 1.5f);
        for (int i = 0; i < 16; i++)
        {
            Vector3 p = up + UnityEngine.Random.insideUnitSphere * radius * 0.6f;
            BulletFX.Mote(BulletFX.Flavor.Embers, fire, p, 2.5f);
            BulletFX.Mote(BulletFX.Flavor.Toxic, poison, p, 2f);
        }
    }

    // ---------- Thermal Shock: Chilled/Frozen + Fire ----------

    static void ThermalShock(Recipe r, Ctx c)
    {
        var t = c.target;
        if (t == null) return;
        var fx = StatusEffects.Of(t);

        // cash in both sides: the ice and the flames
        if (c.consumed == ElementStatus.Burning)
        {
            if (fx.IsFrozen) fx.Consume(ElementStatus.Frozen);
            else fx.ClearFreeze();
        }
        else fx.Consume(ElementStatus.Burning);

        Hurt(t, r.damage + r.damagePerCounter * c.counters, c);

        Color frost = Elements.ColorOf(Element.Frost), fire = Elements.ColorOf(Element.Fire);
        Vector3 chest = AbilityKit.Chest(t);
        float power = Mathf.Clamp01(c.counters / 5f);
        PowerFx.Flash(chest, Color.white, 10f + 6f * power, 6f, 0.3f);
        PowerFx.IceShards(chest, frost, 12 + 4 * c.counters, 7f + 3f * power, 0.2f);
        PowerFx.Sparks(chest, fire, 20 + 6 * c.counters, 8f, 0.5f, 0.08f, 0.8f);
        PowerFx.Puffs(chest, Color.white, 10, 3.5f, 1f, 0.7f, additive: false, lift: 2f);   // flash-boiled steam
        AbilityKit.Shockwave(AbilityKit.Ground(chest), 1.6f + power, Color.white, 0.25f);
    }

    // ---------- Brittle: Water + Frost ----------

    static void Brittle(Recipe r, Ctx c)
    {
        // frost met a water zone: it freezes over into slippery ice
        if (c.target == null)
        {
            if (c.zone is GroundHazard water) water.FreezeOver(c.attacker, r.duration, r.traction);
            return;
        }

        var fx = StatusEffects.Of(c.target);
        int counters = fx.FreezeAtLeast(r.freezeTo);
        Hurt(c.target, r.damage, c);

        // puddles around them freeze too
        foreach (var zone in ElementZones.Overlapping(c.target.transform.position, r.radius, Element.Water))
            if (zone is GroundHazard water) water.FreezeOver(c.attacker, r.duration, r.traction);

        Color frost = Elements.ColorOf(Element.Frost), water2 = Elements.ColorOf(Element.Water);
        Vector3 chest = AbilityKit.Chest(c.target);
        PowerFx.IceShards(chest, frost, 8 + 3 * counters, 4f, 0.16f);
        PowerFx.Sparks(chest, Color.Lerp(frost, Color.white, 0.5f), 18, 4f, 0.5f, 0.06f, 0.3f);
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = chest + UnityEngine.Random.insideUnitSphere * 0.7f;
            BulletFX.Mote(BulletFX.Flavor.Frost, frost, p, 1.5f);
            BulletFX.Mote(BulletFX.Flavor.Water, water2, p, 1.2f);
        }
    }

    // ---------- Mudslide: Water + Earth ----------

    static void Mudslide(Recipe r, Ctx c)
    {
        if (c.target == null)
        {
            if (c.zone is GroundHazard water) water.MudOver(c.attacker, r.duration, r.slow);
            return;
        }

        Hurt(c.target, r.damage, c);
        Vector3 at = AbilityKit.Ground(c.target.transform.position + Vector3.up);
        var mud = GroundHazard.Spawn(c.attacker, at, r.radius, r.duration, Elements.ColorOf(Element.Earth));
        mud.element = Element.Earth;
        mud.slowMultiplier = r.slow;
        mud.blocksDash = true;
        MudFx(at, r.radius);
    }

    internal static void MudFx(Vector3 at, float radius)
    {
        Color earth = Elements.ColorOf(Element.Earth), water = Elements.ColorOf(Element.Water);
        Color mud = Color.Lerp(earth, new Color(0.25f, 0.17f, 0.1f), 0.6f);
        PowerFx.Puffs(at + Vector3.up * 0.4f, mud, 12, 2.5f, 1.1f, 1f, lift: 0.3f);
        for (int i = 0; i < 18; i++)
        {
            Vector3 p = at + Vector3.up * 0.3f + UnityEngine.Random.insideUnitSphere * radius * 0.7f;
            BulletFX.Mote(BulletFX.Flavor.Grit, earth, p, 1.8f);
            if (i % 3 == 0) BulletFX.Mote(BulletFX.Flavor.Water, water, p, 1.2f);
        }
    }

    // ---------- Wildfire: Fire + Nature ----------

    static void Wildfire(Recipe r, Ctx c)
    {
        // fire met growth on the ground: it catches, and spreads to whatever it touches
        if (c.target == null)
        {
            if (c.zone is GroundHazard growth) growth.Ignite(c.attacker, r.duration, r.zoneDps * c.scale);
            return;
        }

        Hurt(c.target, r.damage, c);
        StatusEffects.Of(c.target).SetBurning(AbilityBurnTime);

        Vector3 at = AbilityKit.Ground(c.target.transform.position + Vector3.up);
        foreach (var zone in ElementZones.At(at, Element.Nature))
            if (zone is GroundHazard growth) growth.Ignite(c.attacker, r.duration, r.zoneDps * c.scale);

        // the vines on them burn into a patch that spreads into any growth it touches
        var burn = GroundHazard.Spawn(c.attacker, at, r.radius, r.duration, Elements.ColorOf(Element.Fire));
        burn.element = Element.Fire;
        burn.damagePerSecond = r.zoneDps * c.scale;
        EffectPool.Spawn(at, r.radius, r.duration, EffectPool.Style.Lava);

        Color fire = Elements.ColorOf(Element.Fire), nature = Elements.ColorOf(Element.Nature);
        Vector3 chest = AbilityKit.Chest(c.target);
        PowerFx.Sparks(chest, fire, 36, 6f, 0.8f, 0.09f, -0.4f, Vector3.up, 120f);
        for (int i = 0; i < 12; i++)
        {
            Vector3 p = at + Vector3.up * 0.4f + UnityEngine.Random.insideUnitSphere * r.radius * 0.6f;
            BulletFX.Mote(BulletFX.Flavor.Embers, fire, p, 2f);
            BulletFX.Mote(BulletFX.Flavor.Spores, nature, p, 1.5f);
        }
    }

    // ---------- Steam: Fire + Water ----------

    static void Steam(Recipe r, Ctx c)
    {
        if (c.target == null) return;
        Hurt(c.target, r.damage, c);
        SteamCloud.Spawn(AbilityKit.Ground(c.target.transform.position + Vector3.up), r.radius, r.duration, r.zoneDps * c.scale, c.attacker);
    }

    // ---------- Runner (coroutines and delays) ----------

    static ReactionRunner runner;

    static ReactionRunner Runner()
    {
        if (runner == null) runner = new GameObject("[Reactions]").AddComponent<ReactionRunner>();
        return runner;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        runner = null;
        depth = 0;
        Reacted = null;
    }
}

[AddComponentMenu("")]
class ReactionRunner : MonoBehaviour
{
    public void After(float delay, Action action) => StartCoroutine(Run(delay, action));

    IEnumerator Run(float delay, Action action)
    {
        yield return new WaitForSeconds(delay);
        action();
    }
}
