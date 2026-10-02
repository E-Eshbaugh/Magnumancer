using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Drops items into brawl matches. Every 8-14s (sooner with more players) a beam marks a
/// spot near the fight, on screen, and a couple of seconds later an item lands there, so
/// everyone converges on it. Also runs the leader crown.
///
/// Added automatically to any match scene (one with a MultiplayerManager) that isn't
/// Zombies. Spots are picked around the living players each time (some maps scroll), or
/// from objects named "DropPoint..." if a map has them.
///
/// Editor testing: 1-9 drops item 1-9 next to player 1, Shift+1-6 items 10-15
/// (ItemBook order), 0 rolls a random drop now.
/// </summary>
public class DropDirector : MonoBehaviour
{
    public static bool Enabled = true;

    [Header("Timing")]
    public float firstDrop = 6f;
    [Tooltip("Seconds between drops with 2 players / with 4 players (±jitter)")]
    public float intervalTwoPlayers = 14f;
    public float intervalFourPlayers = 8f;
    [Range(0f, 0.5f)] public float jitter = 0.12f;
    [Tooltip("Beam warning before the item lands")]
    public float telegraph = 2f;

    [Header("Field")]
    public int maxOnField = 4;
    [Tooltip("An untouched pickup disappears after this long")]
    public float pickupLifetime = 20f;
    [Tooltip("Drops land at least this far from every player (when there's room)")]
    public float minPlayerDistance = 2.5f;
    [Tooltip("...and at least this far from other drops")]
    public float minDropSpacing = 4f;

    public static DropDirector Instance { get; private set; }
    public float MatchTime => Time.time - matchStart;

    float matchStart, nextDrop;
    int maxAliveSeen;
    bool finished;
    ItemId? lastDrop;
    readonly List<DropBeam> incoming = new();
    Transform[] designerPoints;

    static readonly List<PlayerHealthControl> inMatch = new(), onField = new();
    static float playersRefreshed = -1f;

    // ---------- bootstrap ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Hook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;   // idempotent across play sessions
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!Enabled || mode != LoadSceneMode.Single) return;
        if (FindAnyObjectByType<MultiplayerManager>() == null) return;   // menus
        if (FindAnyObjectByType<GoblinSpawner>() != null) return;        // Zombies has its own economy
        new GameObject("[DropDirector]").AddComponent<DropDirector>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instance = null;
        inMatch.Clear();
        onField.Clear();
        playersRefreshed = -1f;
        Enabled = true;
    }

    void Awake()
    {
        Instance = this;
        matchStart = Time.time;
        nextDrop = Time.time + firstDrop;

        var points = new List<Transform>();
        foreach (var t in FindObjectsByType<Transform>())
            if (t.name.StartsWith("DropPoint")) points.Add(t);
        designerPoints = points.ToArray();

        gameObject.AddComponent<LeaderCrown>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ---------- players ----------

    /// Players still in the match (lives left), including anyone mid-respawn
    public static List<PlayerHealthControl> PlayersInMatch()
    {
        if (Time.time - playersRefreshed > 0.25f || inMatch.Exists(p => p == null))
        {
            playersRefreshed = Time.time;
            inMatch.Clear();
            foreach (var h in FindObjectsByType<PlayerHealthControl>())
                if (h.IsStanding) inMatch.Add(h);
        }
        inMatch.RemoveAll(p => p == null || !p.IsStanding);
        return inMatch;
    }

    /// Players in the match and on the field right now (not invisible mid-respawn)
    public static List<PlayerHealthControl> LivingPlayers()
    {
        onField.Clear();
        foreach (var p in PlayersInMatch())
            if (!WizardSpawnEffect.IsArriving(p.transform)) onField.Add(p);
        return onField;
    }

    // ---------- drops ----------

    void Update()
    {
        incoming.RemoveAll(b => b == null);
        DebugKeys();
        if (finished) return;

        int alive = PlayersInMatch().Count;
        maxAliveSeen = Mathf.Max(maxAliveSeen, alive);
        // the match is decided: no more drops (solo testing keeps them coming)
        if (maxAliveSeen >= 2 && alive <= 1) { finished = true; return; }
        if (LivingPlayers().Count == 0 || Time.time < nextDrop) return;

        int players = Mathf.Max(2, maxAliveSeen);
        float interval = Mathf.Lerp(intervalTwoPlayers, intervalFourPlayers, Mathf.Clamp01((players - 2) / 2f));
        nextDrop = Time.time + interval * Random.Range(1f - jitter, 1f + jitter);

        var def = ItemBook.Roll(MatchTime, lastDrop);
        if (Drop(def)) lastDrop = def.id;
        else nextDrop = Time.time + 3f;   // nowhere clear on screen right now: try again shortly
    }

    /// Sends this item in now (beam, then landing). False if there was nowhere to put it.
    public bool Drop(ItemBook.Def def)
    {
        // full field: the oldest pickup makes room
        var onField = Pickup.Live;
        if (onField.Count + incoming.Count >= maxOnField && onField.Count > 0)
        {
            Pickup oldest = null;
            foreach (var p in onField) if (p != null && (oldest == null || p.Born < oldest.Born)) oldest = p;
            if (oldest != null) Destroy(oldest.gameObject);
        }

        if (!FindSpot(out var spot)) return false;
        incoming.Add(DropBeam.Spawn(def, spot, telegraph, pickupLifetime));
        return true;
    }

    bool FindSpot(out Vector3 spot)
    {
        spot = default;
        var players = LivingPlayers();
        if (players.Count == 0) return false;

        Vector3 center = Vector3.zero;
        foreach (var p in players) center += p.transform.position;
        center /= players.Count;
        float spread = 0f;
        foreach (var p in players) spread = Mathf.Max(spread, Flat(p.transform.position - center));
        float refY = center.y;

        // a map's own drop points, if it has any (on screen first)
        if (designerPoints.Length > 0)
        {
            var good = new List<Vector3>();
            foreach (var t in designerPoints)
                if (t != null && OnScreen(t.position) && FarFromDrops(t.position)) good.Add(AbilityKit.Ground(t.position + Vector3.up));
            if (good.Count == 0)
                foreach (var t in designerPoints) if (t != null) good.Add(AbilityKit.Ground(t.position + Vector3.up));
            if (good.Count > 0) { spot = good[Random.Range(0, good.Count)]; return true; }
        }

        // otherwise: a clear patch of floor near the middle of the fight, not on top of anyone
        float best = float.MinValue;
        bool found = false;
        float reach = Mathf.Max(6f, spread + 3f);
        for (int i = 0; i < 48; i++)
        {
            Vector2 r = Random.insideUnitCircle * reach;
            Vector3 p = center + new Vector3(r.x, 0f, r.y);
            if (!TryFloor(p, refY, out var floor) || !OnScreen(floor)) continue;

            float nearest = float.MaxValue;
            foreach (var pl in players) nearest = Mathf.Min(nearest, Flat(pl.transform.position - floor));
            float score = Mathf.Min(nearest, 5f) - 0.35f * Flat(floor - center);
            if (nearest < minPlayerDistance) score -= 6f;
            if (!FarFromDrops(floor)) score -= 10f;
            if (score > best) { best = score; spot = floor; found = true; }
        }
        return found;
    }

    bool FarFromDrops(Vector3 p)
    {
        foreach (var d in Pickup.Live) if (d != null && Flat(d.transform.position - p) < minDropSpacing) return false;
        foreach (var b in incoming) if (b != null && Flat(b.Ground - p) < minDropSpacing) return false;
        return true;
    }

    static readonly RaycastHit[] hits = new RaycastHit[16];

    // Solid, flat, open floor at about the players' height (not a prop's top, not under a roof)
    static bool TryFloor(Vector3 p, float refY, out Vector3 floor)
    {
        floor = default;
        Vector3 from = new Vector3(p.x, refY + 3f, p.z);
        int n = Physics.RaycastNonAlloc(from, Vector3.down, hits, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        RaycastHit? ground = null;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.distance >= bestDist) continue;
            var root = DamageEvents.RootOf(h.collider);
            if (DamageEvents.IsCombatant(root) || h.collider.GetComponentInParent<PlayerMovement3D>() != null) continue;
            if (h.collider.GetComponentInParent<Bullet>() != null) continue;
            bestDist = h.distance;
            ground = h;
        }
        if (ground == null) return false;
        var g = ground.Value;
        if (g.normal.y < 0.8f || Mathf.Abs(g.point.y - refY) > 2.5f) return false;
        if (g.collider.GetComponentInParent<Destructible>() != null) return false;
        if (g.collider.attachedRigidbody != null && !g.collider.attachedRigidbody.isKinematic) return false;
        // room to stand: nothing solid where the item floats
        if (Physics.CheckCapsule(g.point + Vector3.up * 0.6f, g.point + Vector3.up * 1.6f, 0.45f,
                                 Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            return false;
        floor = g.point;
        return true;
    }

    static bool OnScreen(Vector3 p)
    {
        var cam = Camera.main;
        if (cam == null) return true;
        Vector3 v = cam.WorldToViewportPoint(p + Vector3.up);
        return v.z > 0f && v.x > 0.1f && v.x < 0.9f && v.y > 0.12f && v.y < 0.85f;
    }

    static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

    // ---------- editor testing ----------

    static readonly Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9 };

    void DebugKeys()
    {
#if UNITY_EDITOR
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit0Key.wasPressedThisFrame) { Drop(ItemBook.Roll(999f)); return; }
        bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        for (int i = 0; i < digits.Length; i++)
        {
            if (!kb[digits[i]].wasPressedThisFrame) continue;
            int index = i + (shift ? 9 : 0);
            if (index >= ItemBook.All.Length) return;
            var player = FirstPlayer();
            if (player == null) return;
            Vector3 at = AbilityKit.Ground(player.transform.position + AbilityKit.AimDir(player.gameObject) * 3f + Vector3.up);
            Pickup.Spawn(ItemBook.All[index], at, pickupLifetime);
            return;
        }
#endif
    }

    static PlayerHealthControl FirstPlayer()
    {
        var players = LivingPlayers();
        PlayerHealthControl first = null;
        foreach (var p in players)
        {
            var m = p.movement != null ? p.movement : p.GetComponent<PlayerMovement3D>();
            if (first == null || (m != null && m.playerIndex == 0)) first = p;
        }
        return first;
    }
}
