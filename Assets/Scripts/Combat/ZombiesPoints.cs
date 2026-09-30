using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Zombies economy, earning side (items-and-drops.md): hits, kills and, with a bonus,
/// elemental reactions and combos earn points, per player. Active only in maps with a
/// GoblinSpawner. Spending (wall buys, doors) should call TrySpend. Big earns pop a small
/// "+60" over the player; there's no HUD total yet (it comes with wall buys).
/// Also puts all players on one team for the Teams hook.
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

    public static int Get(GameObject player) => player != null && points.TryGetValue(player, out int p) ? p : 0;

    public static bool TrySpend(GameObject player, int cost)
    {
        if (!Active || Get(player) < cost) return false;
        Add(player, -cost, false);
        return true;
    }

    static void Add(GameObject player, int amount, bool show)
    {
        if (player == null || player.GetComponent<PlayerHealthControl>() == null) return;
        if (amount > 0) amount = Mathf.RoundToInt(amount * Multiplier);
        points[player] = Get(player) + amount;
        PointsChanged?.Invoke(player, points[player], amount);
        if (show && amount > 0)
        {
            Color theme = AbilityKit.Theme(player);
            ReactionPopup.Show($"+{amount}", theme, Color.white, player.transform.position + Vector3.up * 2.4f, 0.5f);
        }
    }

    static bool IsMonster(GameObject go) => go != null && go.CompareTag("Monster");

    static void OnDamaged(GameObject victim, GameObject attacker, float amount)
    {
        if (Active && amount > 0f && IsMonster(victim)) Add(attacker, PerHit, false);
    }

    static void OnKilled(GameObject victim, GameObject attacker, Vector3 position)
    {
        if (Active && IsMonster(victim)) Add(attacker, PerKill, true);
    }

    static void OnReacted(Reaction reaction, GameObject attacker, GameObject target, Vector3 point)
    {
        if (Active) Add(attacker, PerReaction, true);
    }

    static void OnComboEnded(GameObject player, int count)
    {
        if (Active) Add(player, PerComboStep * count, true);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
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
        ElementReactions.Reacted += OnReacted;      // reset each session
        ReactionCombo.ComboEnded += OnComboEnded;   // reset each session
    }
}
