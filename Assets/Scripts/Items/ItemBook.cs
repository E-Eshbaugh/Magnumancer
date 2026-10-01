using System.Collections.Generic;
using UnityEngine;

/// Every item that can drop. See Docs/Design/items-and-drops.md.
public enum ItemId
{
    RuneShard, HealingDraught, OverdriveOrb, ElementalRounds, BlinkCharm, AegisSigil,
    HexGrenade, StickyBomb, PortalStone,
    SingularityLauncher, FrostCannon, ThunderMaul, GaleHorn, EmberMinigun,
    HeartRelic
}

public enum Rarity { Common, Uncommon, Rare, VeryRare }

public enum ItemKind { Instant, Buff, Throwable, Wonder }

/// <summary>
/// The item table: what each drop is called, how rare it is, its color and how often it
/// rolls. Data lives here in code (like RuneBook and the reaction table) so tuning is one
/// file. Weights are for brawl modes (Deathmatch, later TDM); a per-mode table can plug
/// in at Roll().
/// </summary>
public static class ItemBook
{
    public class Def
    {
        public ItemId id;
        public string name;
        public Rarity rarity;
        public ItemKind kind;
        public Color color;
        public float weight;
        [Tooltip("Only drops once the match has run this long (long-match items)")]
        public float minMatchTime;
    }

    public static readonly Def[] All =
    {
        new Def { id = ItemId.RuneShard,       name = "Rune Shard",       rarity = Rarity.Common,   kind = ItemKind.Instant,   color = new Color(0.7f, 0.45f, 1f),  weight = 14f },
        new Def { id = ItemId.HealingDraught,  name = "Healing Draught",  rarity = Rarity.Common,   kind = ItemKind.Instant,   color = new Color(1f, 0.3f, 0.35f),  weight = 14f },
        new Def { id = ItemId.HexGrenade,      name = "Hex Grenade",      rarity = Rarity.Common,   kind = ItemKind.Throwable, color = new Color(1f, 0.35f, 0.8f),  weight = 6f },
        new Def { id = ItemId.StickyBomb,      name = "Goblin Bomb",      rarity = Rarity.Common,   kind = ItemKind.Throwable, color = new Color(0.55f, 1f, 0.2f),  weight = 6f },
        new Def { id = ItemId.PortalStone,     name = "Portal Stone",     rarity = Rarity.Common,   kind = ItemKind.Throwable, color = new Color(0.3f, 0.75f, 1f),  weight = 5f },
        new Def { id = ItemId.OverdriveOrb,    name = "Overdrive",        rarity = Rarity.Uncommon, kind = ItemKind.Buff,      color = new Color(1f, 0.55f, 0.1f),  weight = 9f },
        new Def { id = ItemId.ElementalRounds, name = "Elemental Rounds", rarity = Rarity.Uncommon, kind = ItemKind.Buff,      color = new Color(1f, 0.9f, 0.3f),   weight = 9f },
        new Def { id = ItemId.BlinkCharm,      name = "Blink Charm",      rarity = Rarity.Uncommon, kind = ItemKind.Buff,      color = new Color(0.2f, 1f, 0.8f),   weight = 8f },
        new Def { id = ItemId.AegisSigil,      name = "Aegis Sigil",      rarity = Rarity.Uncommon, kind = ItemKind.Buff,      color = new Color(0.75f, 0.85f, 1f), weight = 9f },
        new Def { id = ItemId.SingularityLauncher, name = "Singularity Launcher", rarity = Rarity.Rare, kind = ItemKind.Wonder, color = new Color(0.6f, 0.3f, 1f),  weight = 1.8f },
        new Def { id = ItemId.FrostCannon,     name = "Frost Cannon",     rarity = Rarity.Rare,     kind = ItemKind.Wonder,    color = new Color(0.55f, 0.9f, 1f),  weight = 1.8f },
        new Def { id = ItemId.ThunderMaul,     name = "Thunder Maul",     rarity = Rarity.Rare,     kind = ItemKind.Wonder,    color = new Color(1f, 0.92f, 0.35f), weight = 1.8f },
        new Def { id = ItemId.GaleHorn,        name = "Gale Horn",        rarity = Rarity.Rare,     kind = ItemKind.Wonder,    color = new Color(0.85f, 1f, 0.95f), weight = 1.8f },
        new Def { id = ItemId.EmberMinigun,    name = "Ember Minigun",    rarity = Rarity.Rare,     kind = ItemKind.Wonder,    color = new Color(1f, 0.4f, 0.1f),  weight = 1.8f },
        new Def { id = ItemId.HeartRelic,      name = "Heart Relic",      rarity = Rarity.VeryRare, kind = ItemKind.Instant,   color = new Color(1f, 0.15f, 0.3f),  weight = 2f, minMatchTime = 180f },
    };

    static Dictionary<ItemId, Def> byId;

    public static Def Get(ItemId id)
    {
        if (byId == null)
        {
            byId = new Dictionary<ItemId, Def>();
            foreach (var d in All) byId[d.id] = d;
        }
        return byId[id];
    }

    /// Gold for rare drops: the beam, the pickup's rings and its label
    public static readonly Color Gold = new Color(1f, 0.8f, 0.25f);

    /// Beam/ring color for a rarity (common and uncommon drops come in white)
    public static Color RarityColor(Rarity r) => r >= Rarity.Rare ? Gold : Color.white;

    /// A weighted random item. matchTime gates long-match items; `avoid` (the last drop)
    /// is rerolled once so the same thing rarely lands twice in a row.
    public static Def Roll(float matchTime, ItemId? avoid = null)
    {
        var pick = RollOnce(matchTime);
        if (avoid.HasValue && pick.id == avoid.Value) pick = RollOnce(matchTime);
        return pick;
    }

    static Def RollOnce(float matchTime)
    {
        float total = 0f;
        foreach (var d in All) if (matchTime >= d.minMatchTime) total += d.weight;
        float r = Random.value * total;
        foreach (var d in All)
        {
            if (matchTime < d.minMatchTime) continue;
            r -= d.weight;
            if (r <= 0f) return d;
        }
        return All[0];
    }
}
