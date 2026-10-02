using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Per-player, per-run Zombies currency. Combat rewards and purchases share one ledger;
/// PointsChanged drives both the crest total and the temporary overhead readout.
/// </summary>
public static class ZombiesPoints
{
    public static int PerHit = 10;
    public static int PerKill = 60;
    public static int PerReaction = 30;
    public static int PerComboStep = 25;   // a combo of N pays N x this when it ends
    /// Double Points power-up: multiply everything earned
    public static float Multiplier = 1f;

    public static bool Active { get; private set; }

    /// player, new total, change
    public static event Action<GameObject, int, int> PointsChanged;

    static readonly Dictionary<GameObject, int> points = new();

    // Child hitboxes/guns resolve to the same wallet as their owning wizard.
    static GameObject Owner(GameObject player)
        => player != null ? player.GetComponentInParent<PlayerHealthControl>()?.gameObject : null;

    public static int Get(GameObject player)
    {
        player = Owner(player);
        return player != null && points.TryGetValue(player, out int p) ? p : 0;
    }

    /// Reject invalid/free purchases and never report success without a debit.
    public static bool TrySpend(GameObject player, int cost)
    {
        player = Owner(player);
        if (!Active || player == null || cost <= 0 || Get(player) < cost ||
            !player.GetComponent<PlayerHealthControl>().IsStanding) return false;
        Change(player, -cost);
        return true;
    }

    static void Earn(GameObject player, int amount)
    {
        player = Owner(player);
        if (!Active || player == null || amount <= 0 ||
            float.IsNaN(Multiplier) || float.IsInfinity(Multiplier) || Multiplier <= 0f) return;
        // Saturate instead of wrapping the wallet during long runs or stacked bonuses.
        double scaled = Math.Round((double)amount * Multiplier);
        int award = (int)Math.Min(int.MaxValue - Get(player), scaled);
        if (award > 0) Change(player, award);
    }

    static void Change(GameObject player, int amount)
    {
        int total = Get(player) + amount;
        points[player] = total;
        PointsChanged?.Invoke(player, total, amount);
    }

    static bool IsMonster(GameObject go) => go != null && go.GetComponentInParent<GoblinHealth>() != null;

    static void OnDamaged(GameObject victim, GameObject attacker, float amount)
    {
        // Pack-a-Punch splash damage pays kills, not hits (no fabricated hit income)
        if (Active && amount > 0f && IsMonster(victim) && !ForgedRunes.DealingSecondary) Earn(attacker, PerHit);
    }

    static void OnKilled(GameObject victim, GameObject attacker, Vector3 position)
    {
        if (Active && IsMonster(victim)) Earn(attacker, PerKill);
    }

    static void OnReacted(Reaction reaction, GameObject attacker, GameObject target, Vector3 point)
    {
        if (Active) Earn(attacker, PerReaction);
    }

    static void OnComboEnded(GameObject player, int count)
    {
        if (Active) Earn(player, (int)Math.Min(int.MaxValue, (long)Math.Max(0, PerComboStep) * Math.Max(0, count)));
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Additive scenery must not erase an ongoing run.
        if (mode == LoadSceneMode.Additive) return;
        points.Clear();
        Multiplier = 1f;
        Active = UnityEngine.Object.FindAnyObjectByType<GoblinSpawner>() != null;
        if (Active) Teams.AllOneTeam();
        else Teams.FreeForAll();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        points.Clear();
        Active = false;
        Multiplier = 1f;
        PointsChanged = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        // idempotent: DamageEvents and SceneManager aren't reset between play sessions
        DamageEvents.Damaged -= OnDamaged; DamageEvents.Damaged += OnDamaged;
        DamageEvents.Killed -= OnKilled; DamageEvents.Killed += OnKilled;
        SceneManager.sceneLoaded -= OnSceneLoaded; SceneManager.sceneLoaded += OnSceneLoaded;
        ElementReactions.Reacted -= OnReacted; ElementReactions.Reacted += OnReacted;
        ReactionCombo.ComboEnded -= OnComboEnded; ReactionCombo.ComboEnded += OnComboEnded;
    }
}
