using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Elemental Ecosystem's elements. Every wizard is one (bullets, abilities and zones
/// carry it), and each leaves its own status on targets. See Docs/Design/elemental-ecosystem.md.
/// </summary>
public enum Element { None, Fire, Frost, Water, Lightning, Earth, Nature, Poison, Void }

/// What an element leaves on a target (tracked in StatusEffects). Reactions consume these.
public enum ElementStatus
{
    None,
    Burning,    // Fire: brands, or a burn timer from fire abilities and lava
    Chilled,    // Frost: 1+ freeze counters
    Frozen,     // Frost: fully frozen (5 counters, Fractalshot freeze, Flash Freeze encase)
    Soaked,     // Water
    Charged,    // Lightning
    Staggered,  // Earth: stuns and heavy knockback
    Rooted,     // Nature
    Poisoned,   // Poison
    Marked      // Void: Soulfracture mark
}

public static class Elements
{
    /// The element a wizard fights with (same mapping as their bullet flavor)
    public static Element Of(WizardData wizard)
        => wizard != null ? Of(BulletFX.FlavorOf(wizard.passive)) : Element.None;

    /// The element of whoever this object belongs to (players; monsters have none)
    public static Element Of(GameObject caster) => Of(AbilityKit.Wizard(caster));

    public static Element Of(BulletFX.Flavor flavor) => flavor switch
    {
        BulletFX.Flavor.Embers => Element.Fire,
        BulletFX.Flavor.Frost => Element.Frost,
        BulletFX.Flavor.Water => Element.Water,
        BulletFX.Flavor.Volt => Element.Lightning,
        BulletFX.Flavor.Grit => Element.Earth,
        BulletFX.Flavor.Spores => Element.Nature,
        BulletFX.Flavor.Toxic => Element.Poison,
        BulletFX.Flavor.Void => Element.Void,
        _ => Element.None
    };

    public static BulletFX.Flavor FlavorOf(Element e) => e switch
    {
        Element.Fire => BulletFX.Flavor.Embers,
        Element.Frost => BulletFX.Flavor.Frost,
        Element.Water => BulletFX.Flavor.Water,
        Element.Lightning => BulletFX.Flavor.Volt,
        Element.Earth => BulletFX.Flavor.Grit,
        Element.Nature => BulletFX.Flavor.Spores,
        Element.Poison => BulletFX.Flavor.Toxic,
        Element.Void => BulletFX.Flavor.Void,
        _ => BulletFX.Flavor.Sparks
    };

    // Fallbacks if a wizard asset can't be found; normally colors come from the
    // wizards' theme colors so reactions match the bullets that caused them.
    static Color Fallback(Element e) => e switch
    {
        Element.Fire => new Color(1f, 0.42f, 0.1f),
        Element.Frost => new Color(0.6f, 0.9f, 1f),
        Element.Water => new Color(0.15f, 0.55f, 1f),
        Element.Lightning => new Color(1f, 0.92f, 0.35f),
        Element.Earth => new Color(0.85f, 0.6f, 0.3f),
        Element.Nature => new Color(0.35f, 1f, 0.35f),
        Element.Poison => new Color(0.6f, 1f, 0.15f),
        Element.Void => new Color(0.7f, 0.3f, 1f),
        _ => Color.white
    };

    static Dictionary<Element, Color> colors;

    /// Bright display color of an element (popup words, status motes, reaction effects)
    public static Color ColorOf(Element e)
    {
        if (colors == null)
        {
            colors = new Dictionary<Element, Color>();
            foreach (var w in Resources.LoadAll<WizardData>("Wizards"))
            {
                var el = Of(w);
                if (el != Element.None && !colors.ContainsKey(el))
                    colors[el] = GlowLine.Brighten(WizardSpawnEffect.ThemeColorOf(w));
            }
        }
        return colors.TryGetValue(e, out var c) ? c : Fallback(e);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => colors = null;
}
