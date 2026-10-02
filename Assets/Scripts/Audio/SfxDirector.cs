using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Wires the game's sound effects to what's happening, without gameplay scripts having to
/// call it: hits, hit markers, kills, lives lost, kill-streak callouts (DamageEvents),
/// per-player sounds (adds PlayerSfx to every player), the match-start gong and the
/// victory/defeat sting, Zombies points, and the War Table in the Great Hall.
/// Boots itself once and survives scene loads; everything per-match resets on each load.
/// </summary>
public class SfxDirector : MonoBehaviour
{
    const float ScanInterval = 0.25f;
    const int SpreeKills = 3;

    static SfxDirector instance;

    // IsStanding (downed players aren't standing) only exists in some versions of PlayerHealthControl
    static readonly PropertyInfo IsStandingProp = typeof(PlayerHealthControl).GetProperty("IsStanding");

    float nextScan;
    bool matchStarted;
    bool matchEnded;
    int maxStanding;
    bool horde;
    readonly Dictionary<GameObject, int> streaks = new();
    readonly List<PlayerHealthControl> players = new();

    // Great Hall
    HallDirector.Phase lastPhase;
    int lastMode = -1, lastMap = -1;
    bool hallSeen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("[SfxDirector]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<SfxDirector>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    void OnEnable()
    {
        DamageEvents.Damaged += OnDamaged;
        DamageEvents.Killed += OnKilled;
        ZombiesPoints.PointsChanged += OnPoints;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ResetMatch();
    }

    void OnDisable()
    {
        DamageEvents.Damaged -= OnDamaged;
        DamageEvents.Killed -= OnKilled;
        ZombiesPoints.PointsChanged -= OnPoints;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) ResetMatch();
        if (!GamePause.IsPaused) AudioListener.pause = false;   // never carry a held pause into a new scene
    }

    void ResetMatch()
    {
        matchStarted = matchEnded = false;
        maxStanding = 0;
        streaks.Clear();
        players.Clear();
        hallSeen = false;
        lastMode = lastMap = -1;
        nextScan = 0f;
    }

    void Update()
    {
        Sfx.Tick();
        Hall();

        if (Time.unscaledTime < nextScan) return;
        nextScan = Time.unscaledTime + ScanInterval;
        ScanPlayers();
    }

    void LateUpdate() => Sfx.FlushLate();

    // ---------- Players & match flow ----------

    void ScanPlayers()
    {
        if (HallDirector.Instance != null) return;   // the Great Hall has no fighters

        players.Clear();
        foreach (var p in FindObjectsByType<PlayerHealthControl>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (p.GetComponent<PlayerMovement3D>() == null) continue;
            players.Add(p);
            if (p.GetComponent<PlayerSfx>() == null) p.gameObject.AddComponent<PlayerSfx>();
        }
        if (players.Count == 0) return;

        int standing = 0;
        foreach (var p in players) if (Standing(p)) standing++;

        if (!matchStarted && standing > 0)
        {
            matchStarted = true;
            horde = FindAnyObjectByType<GoblinSpawner>() != null;
            Sfx.Play(SfxId.MatchStart);
        }
        maxStanding = Mathf.Max(maxStanding, standing);

        // Mirrors WinManager: last wizard standing wins a brawl; the horde wins when everyone's down
        if (!matchEnded && matchStarted)
        {
            if (!horde && maxStanding >= 2 && standing <= 1)
            {
                matchEnded = true;
                Sfx.Play(standing == 1 ? SfxId.Victory : SfxId.Defeat);
            }
            else if (maxStanding > 0 && standing == 0)
            {
                matchEnded = true;
                Sfx.Play(SfxId.Defeat);
            }
        }
    }

    static bool Standing(PlayerHealthControl p)
    {
        if (p == null || !p.isActiveAndEnabled || p.IsDead) return false;
        return IsStandingProp == null || (bool)IsStandingProp.GetValue(p);
    }

    // ---------- Combat ----------

    static bool IsPlayer(GameObject go) => go != null && go.GetComponent<PlayerHealthControl>() != null;

    void OnDamaged(GameObject victim, GameObject attacker, float amount)
    {
        if (victim == null) return;
        Vector3 at = victim.transform.position;
        var player = victim.GetComponent<PlayerHealthControl>();

        if (amount <= 0f)
        {
            // fully soaked up by a shield
            if (player != null && victim.GetComponent<AegisShieldBuff>() != null) Sfx.Play(SfxId.ShieldBlock, at);
            return;
        }

        if (player != null)
            Sfx.Play(SfxId.Hurt, at, Mathf.Clamp(0.55f + 2f * amount / Mathf.Max(1f, player.maxHealth), 0.55f, 1.2f));
        else if (victim.GetComponentInParent<GoblinHealth>() != null)
            Sfx.Play(SfxId.MonsterHit, at);
        else return;

        // the shooter's hit marker
        if (attacker != null && attacker != victim && IsPlayer(attacker))
            Sfx.Play(SfxId.HitMarker, at, player != null ? 1f : 0.6f);
    }

    void OnKilled(GameObject victim, GameObject attacker, Vector3 at)
    {
        if (victim == null) return;
        bool playerDied = IsPlayer(victim);

        if (playerDied)
        {
            Sfx.Play(SfxId.LifeLost, at);
            streaks.Remove(victim);
        }
        else Sfx.Play(SfxId.MonsterDeath, at);

        if (attacker == null || attacker == victim || !IsPlayer(attacker)) return;

        if (!playerDied)
        {
            Sfx.Play(SfxId.KillConfirm, at, 0.35f, 1.15f);   // a lighter ding per zombie
            return;
        }

        Sfx.Play(SfxId.KillConfirm, at);

        // announcer: kill streaks (3, 6, 9...) and sniper picks
        streaks.TryGetValue(attacker, out int n);
        streaks[attacker] = ++n;
        var gun = attacker.GetComponentInChildren<AmmoControl>();
        if (n % SpreeKills == 0) Sfx.Play(SfxId.KillingSpree);
        else if (gun != null && gun.currentGun != null && gun.currentGun.weaponClass == WeaponClass.Sniper)
            Sfx.Play(SfxId.Headshot);
    }

    void OnPoints(GameObject player, int total, int delta)
    {
        Vector3 at = player != null ? player.transform.position : Vector3.zero;
        if (delta < 0) Sfx.Play(SfxId.Purchase, at);
        else if (delta > 0) Sfx.Play(SfxId.Points, at, Mathf.Clamp(0.35f + delta / 120f, 0.35f, 1f));
    }

    // ---------- Great Hall ----------

    void Hall()
    {
        var hall = HallDirector.Instance;
        if (hall == null) return;

        var phase = hall.CurrentPhase;
        int mode = hall.SelectedMode;
        int map = hall.table != null ? hall.table.Selected : -1;
        if (!hallSeen)
        {
            hallSeen = true;
            lastPhase = phase; lastMode = mode; lastMap = map;
            return;
        }

        if (phase != lastPhase)
        {
            if (phase == HallDirector.Phase.ToTable) Sfx.Play(SfxId.UiReady);
            else if (phase == HallDirector.Phase.Launch) Sfx.Play(SfxId.UiLaunch);
            else if (phase == HallDirector.Phase.Hall) Sfx.Play(SfxId.UiBack);
        }
        else if (phase == HallDirector.Phase.Table && (map != lastMap || mode != lastMode))
            Sfx.Play(SfxId.UiMove);

        lastPhase = phase; lastMode = mode; lastMap = map;
    }
}
