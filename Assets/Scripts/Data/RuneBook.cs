using System;
using UnityEngine;

/// <summary>
/// Every wizard's build options, picked at wizard select:
///  • 3 active runes — Rune I is the wizard's original ability (their prefab + asset text),
///    Runes II and III are alternatives built in code.
///  • 2 passive runes — Rune I is the original passive, Rune II an alternative.
///  • a weapon affinity — a bonus while holding a gun of the wizard's favoured class(es).
/// Keyed by the wizard's faction (WizardData.passive identifies the faction).
/// All rune names, descriptions and cooldowns live here so they're tuned in one place.
/// </summary>
public static class RuneBook
{
    public class ActiveRune
    {
        public string name;
        public string description;
        public float cooldown;       // ignored for Rune I (uses WizardData.abilityCooldown)
        public Type abilityType;     // null = the wizard's own activeAbilityPrefab
    }

    public class PassiveRune
    {
        public string name;
        public string description;
        public Type passiveType;     // null = the wizard's own passive (WizardData.passive)
    }

    public class Affinity
    {
        public WeaponClass[] classes; // empty = every class counts
        public string name;
        public string description;
    }

    public class Runes
    {
        public ActiveRune[] actives;
        public PassiveRune[] passives;
        public Affinity affinity;
    }

    public const int ActiveCount = 3;
    public const int PassiveCount = 2;

    public static string Numeral(int i) => i switch { 0 => "I", 1 => "II", 2 => "III", _ => (i + 1).ToString() };

    public static Runes For(WizardData wizard)
    {
        if (wizard == null) return null;
        var r = Define(wizard.passive);
        if (r == null) return null;

        // Rune I comes from the wizard asset itself
        Split(wizard.activeAbilityTxt, out r.actives[0].name, out r.actives[0].description);
        r.actives[0].cooldown = wizard.abilityCooldown;
        Split(wizard.passiveAbilityTxt, out r.passives[0].name, out r.passives[0].description);
        return r;
    }

    public static ActiveRune Active(WizardData wizard, int index)
    {
        var r = For(wizard);
        return r != null ? r.actives[Mathf.Clamp(index, 0, r.actives.Length - 1)] : null;
    }

    public static PassiveRune Passive(WizardData wizard, int index)
    {
        var r = For(wizard);
        return r != null ? r.passives[Mathf.Clamp(index, 0, r.passives.Length - 1)] : null;
    }

    public static Affinity AffinityOf(WizardData wizard) => For(wizard)?.affinity;

    public static bool Favors(WizardData wizard, WeaponData weapon)
    {
        var a = AffinityOf(wizard);
        if (a == null || weapon == null) return false;
        if (a.classes.Length == 0) return true;
        return Array.IndexOf(a.classes, weapon.weaponClass) >= 0;
    }

    public static string ClassList(Affinity a)
    {
        if (a == null) return "";
        if (a.classes.Length == 0) return "Any weapon";
        var names = new string[a.classes.Length];
        for (int i = 0; i < names.Length; i++) names[i] = Plural(a.classes[i]);
        return string.Join(" & ", names);
    }

    static string Plural(WeaponClass c) => c switch
    {
        WeaponClass.Sniper => "Snipers",
        WeaponClass.Rifle => "Rifles",
        WeaponClass.SMG => "SMGs",
        WeaponClass.Shotgun => "Shotguns",
        WeaponClass.Heavy => "Heavy weapons",
        WeaponClass.Launcher => "Launchers",
        _ => c.ToString()
    };

    static void Split(string text, out string name, out string description)
    {
        text = (text ?? "").Trim();
        int colon = text.IndexOf(':');
        if (colon > 0) { name = text.Substring(0, colon).Trim(); description = text.Substring(colon + 1).Trim(); }
        else { name = "Rune I"; description = text; }
    }

    static ActiveRune A(string name, float cd, Type type, string desc)
        => new ActiveRune { name = name, cooldown = cd, abilityType = type, description = desc };

    static PassiveRune P(string name, Type type, string desc)
        => new PassiveRune { name = name, passiveType = type, description = desc };

    static Affinity F(string name, string desc, params WeaponClass[] classes)
        => new Affinity { name = name, description = desc, classes = classes };

    static ActiveRune Original() => new ActiveRune();
    static PassiveRune OriginalPassive() => new PassiveRune();

    static Runes Define(PassiveType faction) => faction switch
    {
        PassiveType.Stonebind => new Runes // Granite Vow
        {
            actives = new[]
            {
                Original(),
                A("Earthwork Parapet", 14f, typeof(EarthworkParapetAbility),
                  "A jagged crag of rock heaves up beneath you, lifting you to the high ground for a few seconds before sinking back into the earth."),
                A("Bastion Stance", 14f, typeof(BastionStanceAbility),
                  "Plant yourself: an arc of stone slabs erupts in front of you and follows your aim, blocking bullets. You can't be moved or knocked back for 5 seconds; dash to break out early."),
            },
            passives = new[]
            {
                OriginalPassive(),
                P("Fortified", typeof(FortifiedPassive),
                  "Finishing a reload grants a 20 point stone shield for 5 seconds."),
            },
            affinity = F("Siege Stance", "+20% damage while standing still.", WeaponClass.Heavy, WeaponClass.Launcher),
        },

        PassiveType.VerdantResurgence => new Runes // Verdant Circle
        {
            actives = new[]
            {
                Original(),
                A("Thornsnare", 10f, typeof(ThornsnareAbility),
                  "Plant a bramble trap ahead. The first enemy to step in is rooted for 1.5 seconds and pricked."),
                A("Siphoning Vine", 14f, typeof(SiphoningVineAbility),
                  "Lash the enemy you're aiming toward with a living vine that drains their life into you for 4 seconds. It snaps if they get away. Fizzles (no cooldown) if nobody's in reach."),
            },
            passives = new[]
            {
                OriginalPassive(),
                P("Thornhide", typeof(ThornhidePassive),
                  "Enemies who hurt you from close range take 25% of that damage back."),
            },
            affinity = F("Grove Guard", "Recover faster: Verdant Resurgence kicks in 2s sooner (or, without it, heal 3/s after 4s untouched).", WeaponClass.Heavy, WeaponClass.Launcher),
        },

        PassiveType.Undercurrent => new Runes // Tidebound
        {
            actives = new[]
            {
                Original(),
                A("Undertow", 13f, typeof(UndertowAbility),
                  "Open a whirlpool ahead for 3 seconds that drags enemies to its churning center."),
                A("Riptide", 12f, typeof(RiptideAbility),
                  "Surge forward in a torrent of water, shoving through anyone in your way. Press again within 3 seconds to rush back to where you started."),
            },
            passives = new[]
            {
                OriginalPassive(),
                P("Tidal Momentum", typeof(TidalMomentumPassive),
                  "Every dash reloads a quarter of your magazine."),
            },
            affinity = F("Crashing Wave", "Reload 40% faster.", WeaponClass.Shotgun),
        },

        PassiveType.BrandOfFlereous => new Runes // Emberguard
        {
            actives = new[]
            {
                Original(),
                A("Inferno Rounds", 14f, typeof(InfernoRoundsAbility),
                  "Superheat your weapon: refill your magazine and deal 35% more damage for 5 seconds."),
                A("Fireball", 13f, typeof(FireballAbility),
                  "Hurl a fireball at the nearest enemy's position. It bursts on impact, detonating every Brand on the enemies it catches, and leaves a pool of lava."),
            },
            passives = new[]
            {
                OriginalPassive(),
                P("Kindling", typeof(KindlingPassive),
                  "Your hits shave time off your ability cooldown. Taking a life refreshes it completely."),
            },
            affinity = F("Forge-Fed", "+15% fire rate.", WeaponClass.Rifle, WeaponClass.SMG),
        },

        PassiveType.LightningReflex => new Runes // Voltborn
        {
            actives = new[]
            {
                Original(),
                A("Chain Surge", 10f, typeof(ChainSurgeAbility),
                  "Hurl lightning at the nearest enemy you're aiming toward. It arcs to up to 2 more and stuns briefly."),
                A("Stormrunner", 16f, typeof(StormrunnerAbility),
                  "Become the storm for 4.5 seconds: your dash recharges almost instantly and blinks you forward, leaving a lightning fence that shocks and stuns anyone who touches it."),
            },
            passives = new[]
            {
                OriginalPassive(),
                P("Overcharge", typeof(OverchargePassive),
                  "Firing without stopping builds charge, up to +30% fire rate."),
            },
            affinity = F("Stormcell", "Dash recharges 35% faster.", WeaponClass.SMG, WeaponClass.Rifle),
        },

        PassiveType.FractalshotShield => new Runes // Frostwarden
        {
            actives = new[]
            {
                Original(),
                A("Winter's Wind", 11f, typeof(WintersWindAbility),
                  "Unleash a freezing gust of icy wind that hurls everyone in front of you far back and chills them."),
                A("Flash Freeze", 14f, typeof(FlashFreezeAbility),
                  "A frost pulse cashes in every freeze counter nearby: chilled enemies are encased in ice (longer and harder the more counters they had), then shatter free."),
            },
            passives = new[]
            {
                OriginalPassive(),
                P("Cold Precision", typeof(ColdPrecisionPassive),
                  "Hits apply freeze counters. You deal +8% damage per freeze counter on your target."),
            },
            affinity = F("Deadeye", "Scope Focus charges twice as fast. +15% damage.", WeaponClass.Sniper),
        },

        PassiveType.VirulentShroud => new Runes // Blightward
        {
            actives = new[]
            {
                Original(),
                A("Plaguebearer", 16f, typeof(PlaguebearerAbility),
                  "Become a walking plague for 5 seconds. Enemies near you build up plague stacks that hurt more the longer they stay close."),
                A("Contagion", 16f, typeof(ContagionAbility),
                  "Infect every enemy near you. Infection burns for 6 seconds and spreads to anyone who gets close."),
            },
            passives = new[]
            {
                OriginalPassive(),
                P("Leech Spores", typeof(LeechSporesPassive),
                  "Heal for 20% of all damage you deal."),
            },
            affinity = F("Toxic Payload", "Your grenades leave a poison puddle where they burst.", WeaponClass.Launcher, WeaponClass.Rifle),
        },

        PassiveType.LastRites => new Runes // The Hollow
        {
            actives = new[]
            {
                Original(),
                A("Soul Swap", 20f, typeof(SoulSwapAbility),
                  "Trade health percentages with the enemy you're aiming toward. Fizzles (no cooldown) if nobody's in reach."),
                A("Void Rift", 16f, typeof(VoidRiftAbility),
                  "Hold to tear a portal at your feet, walk, and release to open its twin (tap: the exit opens ahead). Linked for 8 seconds; anyone can use them, you come out faster."),
            },
            passives = new[]
            {
                OriginalPassive(),
                P("Soul Harvest", typeof(SoulHarvestPassive),
                  "Each life you take grants +6% damage (up to +30%) until you lose one of your own."),
            },
            affinity = F("Magnumancer", "+4% damage for each different weapon class in your loadout."),
        },

        _ => null
    };
}
