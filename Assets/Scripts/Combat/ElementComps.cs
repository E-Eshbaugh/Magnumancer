using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Team roles and named comps from the Elemental Ecosystem (§4), for the wizard select
/// screen: each wizard's role, the comps they're part of, and (in co-op / team modes) a
/// callout when someone who already picked completes a comp.
/// </summary>
public static class ElementComps
{
    public class Comp
    {
        public string name, pitch;
        public PassiveType a, b;   // b = None: pairs with anyone
    }

    public static readonly Comp[] Comps =
    {
        new Comp { name = "Storm Front",   a = PassiveType.Undercurrent,      b = PassiveType.LightningReflex,   pitch = "Soak them, then Conduct" },
        new Comp { name = "Frostbreaker",  a = PassiveType.FractalshotShield, b = PassiveType.Stonebind,         pitch = "Freeze them, then Shatter" },
        new Comp { name = "Pyre",          a = PassiveType.VirulentShroud,    b = PassiveType.BrandOfFlereous,   pitch = "Gas them, then Combust" },
        new Comp { name = "Monsoon Grove", a = PassiveType.Undercurrent,      b = PassiveType.VerdantResurgence, pitch = "Water makes the growth surge" },
        new Comp { name = "Void Echo",     a = PassiveType.LastRites,         b = PassiveType.None,              pitch = "Every reaction on the Marked Echoes" },
    };

    public static string RoleOf(PassiveType p) => p switch
    {
        PassiveType.Undercurrent or PassiveType.FractalshotShield or PassiveType.VirulentShroud => "Setup",
        PassiveType.LightningReflex or PassiveType.BrandOfFlereous or PassiveType.Stonebind => "Detonator",
        PassiveType.LastRites => "Amplifier",
        PassiveType.VerdantResurgence => "Support",
        _ => ""
    };

    /// Two wizards form a comp together
    public static Comp Between(WizardData x, WizardData y)
    {
        if (x == null || y == null || x == y) return null;
        foreach (var c in Comps)
        {
            if (c.b == PassiveType.None)
            {
                if (x.passive == c.a || y.passive == c.a) return c;
                continue;
            }
            if ((x.passive == c.a && y.passive == c.b) || (x.passive == c.b && y.passive == c.a)) return c;
        }
        return null;
    }

    /// One line for the select screen (legacy UI Text rich text; color tags only, no <b>:
    /// the description uses the Bone bitmap font, which can't do bold). `picked` = wizards
    /// locked in earlier this pick, by slot; `teamMode` = co-op/teams, where comps matter.
    public static string Hint(WizardData wizard, IList<(int slot, WizardData wizard)> picked, bool teamMode)
    {
        if (wizard == null) return "";
        var sb = new StringBuilder();

        // someone already locked in completes a comp with this wizard
        if (teamMode && picked != null)
            foreach (var (slot, other) in picked)
            {
                var c = Between(wizard, other);
                if (c == null) continue;
                sb.Append($"<color=#FFD24A>{c.name.ToUpper()} with P{slot + 1}'s {other.wizardName}! {c.pitch}.</color>\n");
                return sb.ToString();
            }

        string role = RoleOf(wizard.passive);
        Color theme = GlowLine.Brighten(WizardSpawnEffect.ThemeColorOf(wizard));
        if (role.Length > 0) sb.Append($"<color=#{ColorUtility.ToHtmlStringRGB(theme)}>{role}</color>");
        foreach (var c in Comps)
        {
            if (wizard.passive != c.a && wizard.passive != c.b) continue;
            string partner = c.b == PassiveType.None ? "anyone"
                           : NameOf(wizard.passive == c.a ? c.b : c.a);
            sb.Append($"  ·  {c.name} (+{partner})");
        }
        if (sb.Length > 0) sb.Append('\n');
        return sb.ToString();
    }

    static string NameOf(PassiveType p)
    {
        foreach (var w in Resources.LoadAll<WizardData>("Wizards"))
            if (w.passive == p) return w.wizardName;
        return p.ToString();
    }
}
