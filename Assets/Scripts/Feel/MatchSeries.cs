using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// The brawl campaign: a tour of maps, one round per map, first to WinsNeeded round wins
/// takes the series. Lives across scene loads (static) and resets whenever the players go
/// back to the Great Hall or the title. Players are tracked by slot (P1..P4).
/// </summary>
public static class MatchSeries
{
    public const int MaxPlayers = 4;

    /// First to this many round wins is champion. TODO: pick 3/5/7 at the War Table.
    public static int WinsNeeded = 3;

    /// PvP maps the tour rotates through. The current map is always in the pool, so a
    /// series works on any arena even if it's missing here. Add arenas as they're ready.
    public static readonly string[] TourMaps = { "CinderCrucible" };

    public static int Round { get; private set; }
    public static readonly int[] Wins = new int[MaxPlayers];
    /// Round wins in a row (resets when someone else takes a round)
    public static readonly int[] Streak = new int[MaxPlayers];
    /// Who took each player out last round (slot, or -1): kill them back for REVENGE
    public static readonly int[] KilledBy = { -1, -1, -1, -1 };
    public static readonly int[] TotalKills = new int[MaxPlayers];
    public static readonly List<string> MapsPlayed = new();

    public static bool InProgress => Round > 0;

    public static int Leader()
    {
        int best = -1;
        for (int i = 0; i < MaxPlayers; i++)
            if (Wins[i] > 0 && (best < 0 || Wins[i] > Wins[best])) best = i;
        return best;
    }

    /// Someone needs one more round win
    public static bool MatchPoint()
    {
        foreach (int w in Wins) if (w == WinsNeeded - 1) return true;
        return false;
    }

    public static int Champion()
    {
        for (int i = 0; i < MaxPlayers; i++) if (Wins[i] >= WinsNeeded) return i;
        return -1;
    }

    public static void BeginRound(string scene)
    {
        Round++;
        MapsPlayed.Add(scene);
    }

    /// winner = slot, or -1 for a draw
    public static void EndRound(int winner, IReadOnlyDictionary<int, int> killedBy, IReadOnlyDictionary<int, int> kills)
    {
        for (int i = 0; i < MaxPlayers; i++)
        {
            KilledBy[i] = killedBy != null && killedBy.TryGetValue(i, out int k) ? k : -1;
            if (kills != null && kills.TryGetValue(i, out int n)) TotalKills[i] += n;
            if (i == winner) { Wins[i]++; Streak[i]++; }
            else Streak[i] = 0;
        }
    }

    /// The next arena: a different one when there's a choice
    public static string NextMap(string current)
    {
        var pool = new List<string>();
        foreach (var m in TourMaps)
            if (m != current && Application.CanStreamedLevelBeLoaded(m)) pool.Add(m);
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : current;
    }

    public static void Reset()
    {
        Round = 0;
        MapsPlayed.Clear();
        for (int i = 0; i < MaxPlayers; i++)
        {
            Wins[i] = Streak[i] = TotalKills[i] = 0;
            KilledBy[i] = -1;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        WinsNeeded = 3;
        Reset();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    // Leaving for the menus ends the series
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == MenuScenes.Hall || scene.name == MenuScenes.Title || scene.name == MenuScenes.LegacyBook)
            Reset();
    }
}
