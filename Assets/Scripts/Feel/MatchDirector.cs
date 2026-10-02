using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Runs a deathmatch round as a show: "ROUND 2 / FIGHT!" on arrival, announcer callouts
/// for first blood, multi-kills, revenge and the final showdown, then a winner spotlight,
/// the series scoreboard, and on to the next map of the tour (MatchSeries) until someone
/// is champion and everyone goes back to the Great Hall.
///
/// Added to any arena with a MultiplayerManager. In PvP it takes over ending the round
/// from WinManager (which it switches off). In Zombies it only does the opening banner;
/// WinManager still ends that mode.
/// </summary>
[AddComponentMenu("")]
public class MatchDirector : MonoBehaviour
{
    const float IntroDelay = 0.9f;      // let the arrival bolts land first
    const float MultiKillWindow = 4f;
    const float BoardTime = 6f;         // results board auto-advances after this
    const float BoardMinTime = 1.5f;    // before a button press can skip it

    class Entry
    {
        public PlayerHealthControl health;
        public int slot;
        public WizardData wizard;
        public Color color;
        public int kills, lastLives;
        public bool tookDamage;
        public readonly List<float> killTimes = new();
        public string Name => $"P{slot + 1} {(wizard != null ? wizard.wizardName.ToUpperInvariant() : "WIZARD")}";
        public string WinsKey => wizard != null ? "wins_" + wizard.wizardName.ToLowerInvariant().Replace(' ', '_') : null;
        public bool Standing => health != null && !health.IsDead && health.isActiveAndEnabled && health.currentHealth > 0f;
    }

    readonly List<Entry> players = new();
    readonly Dictionary<int, int> killedBy = new();
    readonly Dictionary<int, int> kills = new();
    bool horde, ended, firstBlood, showdown, gathered;
    int maxStanding;
    float startTime, nextWinManagerCheck;

    // ---------- Boot ----------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        TryAdd();   // the scene that was already open when play started
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { if (mode == LoadSceneMode.Single) TryAdd(); }

    static void TryAdd()
    {
        if (FindAnyObjectByType<MatchDirector>() != null) return;
        if (FindAnyObjectByType<MultiplayerManager>() == null) return;
        new GameObject("[MatchDirector]").AddComponent<MatchDirector>();
    }

    void OnEnable()
    {
        DamageEvents.Killed += OnKilled;
        DamageEvents.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        DamageEvents.Killed -= OnKilled;
        DamageEvents.Damaged -= OnDamaged;
    }

    void Start()
    {
        startTime = Time.unscaledTime;
        horde = FindAnyObjectByType<GoblinSpawner>() != null;
        FeelAudio.ClearAnnouncer();
        if (!horde) MatchSeries.BeginRound(SceneManager.GetActiveScene().name);
        MusicDirector.SetIntensity(horde ? 1 : 0);   // the horde is pressure from the start
        StartCoroutine(Intro());
    }

    // ---------- Intro ----------

    IEnumerator Intro()
    {
        yield return new WaitForSecondsRealtime(IntroDelay);
        Color gold = new Color(1f, 0.82f, 0.35f);
        if (horde)
        {
            FeelHud.Banner("HOLD THE LINE", gold, "SURVIVE THE HORDE", 1.6f);
            FeelAudio.Announce("fight", important: true);
            yield break;
        }

        int round = MatchSeries.Round;
        bool matchPoint = MatchSeries.MatchPoint();
        string sub = matchPoint ? "MATCH POINT" : $"FIRST TO {MatchSeries.WinsNeeded}";
        FeelHud.Banner($"ROUND {round}", gold, sub, 1.0f);
        FeelAudio.Announce(FeelAudio.HasLine($"round_{round}") ? $"round_{round}" : "round_final", important: true);
        if (matchPoint) FeelAudio.Announce("match_point", important: true);

        yield return new WaitForSecondsRealtime(matchPoint ? 2.1f : 1.25f);
        FeelHud.Banner("FIGHT!", new Color(1f, 0.45f, 0.25f), null, 0.6f, 1.2f);
        FeelAudio.Announce("fight", important: true);
    }

    // ---------- Tracking ----------

    void Gather()
    {
        players.Clear();
        foreach (var h in FindObjectsByType<PlayerHealthControl>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var mover = h.GetComponent<PlayerMovement3D>();
            if (mover == null) mover = h.GetComponentInParent<PlayerMovement3D>();
            var e = new Entry
            {
                health = h,
                slot = mover != null ? mover.playerIndex : players.Count,
                wizard = mover != null ? mover.wizard : null,
                lastLives = h.LivesLeft,
            };
            e.color = GlowLine.Brighten(WizardShade.Of(h.gameObject));
            players.Add(e);
        }
        players.Sort((a, b) => a.slot.CompareTo(b.slot));
    }

    Entry Find(GameObject go)
    {
        if (go == null) return null;
        var h = go.GetComponentInParent<PlayerHealthControl>();
        if (h == null) return null;
        foreach (var e in players) if (e.health == h) return e;
        return null;
    }

    void OnDamaged(GameObject victim, GameObject attacker, float amount)
    {
        if (ended || amount <= 0f) return;
        var e = Find(victim);
        if (e != null) e.tookDamage = true;
    }

    void OnKilled(GameObject victim, GameObject attacker, Vector3 at)
    {
        if (ended || horde) return;
        var v = Find(victim);
        if (v == null) return;
        var a = Find(attacker);
        if (a == null || a == v) return;   // fell, or their own blast: no credit

        a.kills++;
        kills[a.slot] = a.kills;
        bool revenge = MatchSeries.KilledBy[a.slot] == v.slot
                    || (killedBy.TryGetValue(a.slot, out int k) && k == v.slot);
        killedBy[v.slot] = a.slot;

        float now = Time.unscaledTime;
        a.killTimes.RemoveAll(t => now - t > MultiKillWindow);
        a.killTimes.Add(now);
        int streak = a.killTimes.Count;

        // the most notable thing gets the voice; text for the rest
        if (streak >= 3) Callout("TRIPLE KILL", a, "triple_kill");
        else if (streak == 2) Callout("DOUBLE KILL", a, "double_kill");
        if (revenge) Callout("REVENGE", a, streak >= 2 ? null : "revenge");
        if (!firstBlood)
        {
            firstBlood = true;
            Callout("FIRST BLOOD", a, streak >= 2 || revenge ? null : "first_blood");
            MusicDirector.SetIntensity(1);
        }
    }

    void Callout(string text, Entry who, string line)
    {
        FeelHud.Callout(text, who.color, who.Name);
        if (line != null) FeelAudio.Announce(line);
    }

    void Update()
    {
        if (horde || ended) return;

        if (Time.unscaledTime >= nextWinManagerCheck)
        {
            nextWinManagerCheck = Time.unscaledTime + 0.25f;
            foreach (var wm in FindObjectsByType<WinManager>(FindObjectsSortMode.None)) wm.enabled = false;
        }

        // Players are switched on by MultiplayerManager's Start; read them a moment later
        if (!gathered)
        {
            if (Time.unscaledTime - startTime < 0.3f) return;
            Gather();
            gathered = true;
        }
        if (players.Count < 2) return;   // solo testing: the round never ends

        int standing = 0;
        foreach (var e in players)
        {
            if (e.Standing) standing++;
            // Last life (only for modes that still have stocks)
            int lives = e.health != null ? e.health.LivesLeft : 0;
            if (lives == 1 && e.lastLives > 1)
            {
                FeelHud.Callout("LAST STAND", e.color, e.Name);
                FeelAudio.Announce("last_stand");
            }
            e.lastLives = lives;
        }
        maxStanding = Mathf.Max(maxStanding, standing);

        if (!showdown && standing == 2 && maxStanding >= 3)
        {
            showdown = true;
            FeelHud.Banner("FINAL SHOWDOWN", new Color(1f, 0.3f, 0.3f), null, 1.0f, 0.85f);
            FeelAudio.Announce("final_showdown", important: true);
            MusicDirector.SetIntensity(2);
        }

        if (standing <= 1 && maxStanding >= 2)
        {
            ended = true;
            StartCoroutine(EndRound());
        }
    }

    // ---------- The end of a round ----------

    IEnumerator EndRound()
    {
        // a blink of grace: two wizards going down to the same blast is a draw
        yield return new WaitForSecondsRealtime(0.25f);
        Entry winner = null;
        int left = 0;
        foreach (var e in players) if (e.Standing) { winner = e; left++; }
        if (left != 1) winner = null;
        if (winner != null && winner.health != null) winner.health.invincible = true;   // no dying on the podium

        MatchSeries.EndRound(winner != null ? winner.slot : -1, killedBy, kills);
        int champion = MatchSeries.Champion();

        // let the elimination slow-mo play out
        yield return new WaitForSecondsRealtime(0.6f);

        if (winner != null)
        {
            MusicDirector.Victory();
            StartCoroutine(Spotlight(winner));
            if (winner.health != null) Rumble.Play(winner.health.gameObject, 0.6f, 1f, 1.2f);
            string sub = !winner.tookDamage ? "FLAWLESS"
                       : MatchSeries.Streak[winner.slot] >= 3 ? "UNSTOPPABLE"
                       : $"WINS ROUND {MatchSeries.Round}";
            FeelHud.Banner(winner.wizard != null ? winner.wizard.wizardName.ToUpperInvariant() : $"PLAYER {winner.slot + 1}",
                winner.color, sub, 2.2f, 0.9f);
            if (winner.WinsKey != null && FeelAudio.HasLine(winner.WinsKey)) FeelAudio.Announce(winner.WinsKey, important: true);
            if (!winner.tookDamage) FeelAudio.Announce("flawless", important: true);
            else if (MatchSeries.Streak[winner.slot] >= 3) FeelAudio.Announce("unstoppable", important: true);
        }
        else
        {
            MusicDirector.Draw();
            FeelHud.Banner("DRAW", new Color(0.75f, 0.75f, 0.8f), "NOBODY SCORES", 2f);
            FeelAudio.Announce("draw", important: true);
        }

        yield return new WaitForSecondsRealtime(2.8f);

        var rows = new List<FeelHud.Row>();
        foreach (var e in players)
            rows.Add(new FeelHud.Row
            {
                name = e.Name, color = e.color, wins = MatchSeries.Wins[Mathf.Clamp(e.slot, 0, MatchSeries.MaxPlayers - 1)],
                kills = e.kills, roundWinner = e == winner,
            });

        bool over = champion >= 0;
        Entry champ = over ? players.Find(p => p.slot == champion) : null;
        string title = over ? $"{(champ != null ? champ.Name : "P" + (champion + 1))} IS CHAMPION" : $"ROUND {MatchSeries.Round}";
        Color titleColor = over && champ != null ? champ.color : new Color(1f, 0.82f, 0.35f);
        FeelHud.ShowBoard(title, titleColor, rows, MatchSeries.WinsNeeded, Footer(over, BoardTime));
        if (over)
        {
            FeelAudio.Announce("champion", important: true);
            if (champ != null) StartCoroutine(Confetti(champ));
        }

        // Count down to the next map (or the Hall); A / Start / Space skips ahead
        float shown = Time.unscaledTime;
        float total = over ? BoardTime + 2f : BoardTime;
        while (true)
        {
            float waited = Time.unscaledTime - shown;
            if (GamePause.IsPaused) { shown += Time.unscaledDeltaTime; yield return null; continue; }
            if (waited >= total || (waited >= BoardMinTime && ContinuePressed())) break;
            FeelHud.SetBoardFooter(Footer(over, total - waited));
            yield return null;
        }

        if (!GamePause.IsPaused) Time.timeScale = 1f;
        SceneManager.LoadScene(over ? MenuScenes.Hall : MatchSeries.NextMap(SceneManager.GetActiveScene().name));
    }

    static string Footer(bool over, float secondsLeft)
    {
        int s = Mathf.Max(1, Mathf.CeilToInt(secondsLeft));
        return over
            ? $"BACK TO THE GREAT HALL IN {s}   ·   PRESS A TO CONTINUE"
            : $"FIRST TO {MatchSeries.WinsNeeded}   ·   NEXT MAP IN {s}   ·   PRESS A TO CONTINUE";
    }

    static bool ContinuePressed()
    {
        foreach (var pad in Gamepad.all)
            if (pad.buttonSouth.wasPressedThisFrame || pad.startButton.wasPressedThisFrame) return true;
        var kb = Keyboard.current;
        return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
    }

    // A column of light and rings on the last wizard standing
    IEnumerator Spotlight(Entry winner)
    {
        if (winner.health == null) yield break;
        var who = winner.health.gameObject;
        Vector3 ground = AbilityKit.Ground(who.transform.position + Vector3.up * 0.5f);

        var lightGo = new GameObject("WinnerSpotlight");
        var spot = lightGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.color = Color.Lerp(winner.color, Color.white, 0.35f);
        spot.spotAngle = 32f;
        spot.range = 30f;
        spot.intensity = 0f;
        spot.shadows = LightShadows.None;

        AbilityKit.Shockwave(ground, 6f, winner.color, 0.7f);
        PowerFx.Flash(AbilityKit.Chest(who), winner.color, 10f, 8f, 0.5f);

        float t = 0f, ring = 0f;
        while (lightGo != null && who != null)
        {
            t += Time.unscaledDeltaTime;
            ring -= Time.unscaledDeltaTime;
            Vector3 feet = who.transform.position;
            lightGo.transform.position = feet + Vector3.up * 14f;
            lightGo.transform.rotation = Quaternion.LookRotation(Vector3.down);
            spot.intensity = Mathf.Lerp(0f, 60f, Mathf.Clamp01(t / 0.4f));
            if (ring <= 0f)
            {
                ring = 0.9f;
                AbilityKit.Shockwave(AbilityKit.Ground(feet + Vector3.up * 0.5f), 3f, winner.color, 0.6f);
                PowerFx.Sparks(AbilityKit.Chest(who) + Vector3.up * 0.8f, winner.color, 14, 5f, 0.8f);
            }
            yield return null;
        }
    }

    IEnumerator Confetti(Entry champ)
    {
        Color gold = new Color(1f, 0.82f, 0.35f);
        for (int i = 0; i < 14 && champ.health != null; i++)
        {
            Vector3 at = AbilityKit.Chest(champ.health.gameObject) + Vector3.up * 2.5f + Random.insideUnitSphere * 1.5f;
            PowerFx.Sparks(at, i % 2 == 0 ? champ.color : gold, 30, 7f, 1.1f);
            yield return new WaitForSecondsRealtime(0.35f);
        }
    }
}
