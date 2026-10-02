using System;
using UnityEngine;

/// <summary>
/// Who's on whose side. Team Deathmatch isn't built yet, so by default everyone is on
/// their own (free-for-all) and nothing is friendly. TDM should set TeamOf when it
/// assigns teams. Zombies puts every player on one co-op team.
///
/// Zombies is strictly humans versus the horde: only a monster can harm a player.
/// Deathmatch keeps its existing free-for-all rules.
/// </summary>
public static class Teams
{
    /// Reactions (and what they leave behind) hurt teammates
    public static bool ReactionFriendlyFire = true;
    /// Bullets hurt teammates. Not enforced yet: wire into Bullet when TDM exists.
    public static bool BulletFriendlyFire = false;
    public static bool HumansVsHorde { get; private set; }

    /// The final guard for damage and hostile effects, including unowned map hazards.
    public static bool CanHarm(GameObject target, GameObject source)
    {
        if (!HumansVsHorde || target == null) return true;
        var monster = source != null ? source.GetComponentInParent<GoblinHealth>() : null;
        if (target.GetComponentInParent<PlayerHealthControl>() != null) return monster != null;
        return target.GetComponentInParent<GoblinHealth>() == null || monster == null;
    }

    /// A player's team, or -1 for nobody's (free-for-all)
    public static Func<GameObject, int> TeamOf = _ => -1;

    public static bool SameTeam(GameObject a, GameObject b)
    {
        if (a == null || b == null || a == b) return false;
        int ta = TeamOf(a);
        return ta >= 0 && ta == TeamOf(b);
    }

    /// Zombies: all players fight the horde together
    public static void AllOneTeam()
    {
        HumansVsHorde = true;
        ReactionFriendlyFire = BulletFriendlyFire = false;
        TeamOf = go => go == null ? -1 : go.GetComponent<PlayerHealthControl>() != null ? 0
            : go.GetComponent<GoblinHealth>() != null ? 1 : -1;
    }

    public static void FreeForAll()
    {
        HumansVsHorde = false;
        ReactionFriendlyFire = true;
        TeamOf = _ => -1;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ReactionFriendlyFire = true;
        BulletFriendlyFire = false;
        HumansVsHorde = false;
        TeamOf = _ => -1;
    }
}
