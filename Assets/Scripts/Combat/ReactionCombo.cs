using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Per-player reaction combo: reactions you set off within a few seconds of each other
/// chain into a combo, called out above your head (DOUBLE REACTION!, TRIPLE...) with
/// escalating rumble. No HUD. ComboChanged is the hook for Zombies points and the
/// announcer; ComboEnded reports the final count.
/// </summary>
public static class ReactionCombo
{
    /// A new reaction extends the combo if it lands within this long of the last one
    public static float Window = 4f;

    /// player, combo count (1 = a single reaction)
    public static event Action<GameObject, int> ComboChanged;
    /// player, final count (only for real combos, 2+)
    public static event Action<GameObject, int> ComboEnded;

    class Streak { public int count; public float last; }
    static readonly Dictionary<GameObject, Streak> streaks = new();

    static readonly string[] Callouts =
    {
        null, null,
        "DOUBLE REACTION!",
        "TRIPLE REACTION!",
        "QUAD REACTION!",
        "ELEMENTAL OVERLOAD!",
    };

    public static int CountOf(GameObject player)
        => player != null && streaks.TryGetValue(player, out var s) && Time.time - s.last <= Window ? s.count : 0;

    static void OnReacted(Reaction reaction, GameObject attacker, GameObject target, Vector3 point)
    {
        if (attacker == null || attacker.GetComponent<PlayerHealthControl>() == null) return;
        Expire();

        if (!streaks.TryGetValue(attacker, out var s)) streaks[attacker] = s = new Streak();
        if (ticker == null) ticker = new GameObject("[ReactionCombo]").AddComponent<ComboTicker>();
        s.count++;
        s.last = Time.time;
        ComboChanged?.Invoke(attacker, s.count);
        if (s.count < 2) return;

        // called out over the player who's cooking, in their own colors
        string word = Callouts[Mathf.Min(s.count, Callouts.Length - 1)];
        if (s.count >= Callouts.Length) word = $"ELEMENTAL OVERLOAD! <size=70%>x{s.count}</size>";
        Color theme = AbilityKit.Theme(attacker);
        float size = 0.8f + 0.1f * Mathf.Min(s.count, 6);
        ReactionPopup.Show(word, theme, Color.Lerp(theme, Color.white, 0.6f), attacker.transform.position + Vector3.up * 3.2f, size);

        float k = Mathf.Clamp01(s.count / 5f);
        Rumble.Play(attacker, 0.4f + 0.6f * k, 0.8f + 0.2f * k, 0.3f + 0.3f * k);
        if (s.count >= 3) CameraShake.Shake(0.12f + 0.06f * Mathf.Min(s.count - 3, 3), 0.25f);
        ReactionAudio.Combo(s.count);
    }

    static ComboTicker ticker;

    // Close out streaks whose window ran out (also ticked every frame, so ComboEnded is on time)
    internal static void Expire()
    {
        if (streaks.Count == 0) return;
        List<GameObject> done = null;
        foreach (var kv in streaks)
            if (kv.Key == null || Time.time - kv.Value.last > Window)
                (done ??= new List<GameObject>()).Add(kv.Key);
        if (done == null) return;
        foreach (var p in done)
        {
            if (p != null && streaks[p].count >= 2) ComboEnded?.Invoke(p, streaks[p].count);
            streaks.Remove(p);
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        streaks.Clear();
        ticker = null;
        ComboChanged = null;
        ComboEnded = null;
    }

    // after ElementReactions clears its own event
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook() => ElementReactions.Reacted += OnReacted;
}

[AddComponentMenu("")]
class ComboTicker : MonoBehaviour
{
    void Update() => ReactionCombo.Expire();
}
