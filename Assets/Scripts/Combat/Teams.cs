using System;
using UnityEngine;

/// <summary>
/// Who's on whose side. Team Deathmatch isn't built yet, so by default everyone is on
/// their own (free-for-all) and nothing is friendly. TDM should set TeamOf when it
/// assigns teams. Zombies puts every player on one co-op team.
///
/// Friendly fire per the design (modes-and-flow.md): bullets off by default, reactions
/// ON (toggle). ReactionFriendlyFire is enforced by ElementReactions; bullets don't
/// check teams yet (that lands with TDM).
/// </summary>
public static class Teams
{
    /// Reactions (and what they leave behind) hurt teammates
    public static bool ReactionFriendlyFire = true;
    /// Bullets hurt teammates. Not enforced yet: wire into Bullet when TDM exists.
    public static bool BulletFriendlyFire = false;

    /// A player's team, or -1 for nobody's (free-for-all)
    public static Func<GameObject, int> TeamOf = _ => -1;

    public static bool SameTeam(GameObject a, GameObject b)
    {
        if (a == null || b == null || a == b) return false;
        int ta = TeamOf(a);
        return ta >= 0 && ta == TeamOf(b);
    }

    /// Zombies: all players fight the horde together
    public static void AllOneTeam() => TeamOf = go => go != null && go.GetComponent<PlayerHealthControl>() != null ? 0 : -1;

    public static void FreeForAll() => TeamOf = _ => -1;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        ReactionFriendlyFire = true;
        BulletFriendlyFire = false;
        TeamOf = _ => -1;
    }
}
