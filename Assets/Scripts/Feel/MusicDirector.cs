using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Music for every scene: the calm hall theme on the title and in the Great Hall, a
/// battle theme in the arenas that builds as the round heats up (MatchDirector raises
/// the intensity on first blood and the final showdown), and a fanfare for the winner.
/// Ducks under the announcer. Uses the clips in Resources/FeelSounds when they're set,
/// otherwise the built-in synth score (ProceduralScore).
/// </summary>
[AddComponentMenu("")]
public class MusicDirector : MonoBehaviour
{
    static MusicDirector instance;

    // transposition per arena visit so back-to-back rounds don't sound identical
    static readonly int[] BattleKeys = { 0, 3, -2, 5, 2 };

    ProceduralScore score;
    AudioSource clipSource, stinger;
    float fade = 1f, fadeTarget = 1f, fadeSpeed = 1f;
    bool usingClip;
    int battleVisits;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("[MusicDirector]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<MusicDirector>();
        instance.Setup();
        instance.SceneStarted(SceneManager.GetActiveScene());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;

    void Setup()
    {
        var synthGo = new GameObject("Score");
        synthGo.transform.SetParent(transform, false);
        var src = synthGo.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        src.ignoreListenerPause = true;
        score = synthGo.AddComponent<ProceduralScore>();
        src.Play();   // with no clip, the score's OnAudioFilterRead is the sound

        clipSource = gameObject.AddComponent<AudioSource>();
        clipSource.playOnAwake = false;
        clipSource.loop = true;
        clipSource.spatialBlend = 0f;
        clipSource.ignoreListenerPause = true;

        stinger = gameObject.AddComponent<AudioSource>();
        stinger.playOnAwake = false;
        stinger.spatialBlend = 0f;
        stinger.ignoreListenerPause = true;
    }

    void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single) SceneStarted(scene);
    }

    void SceneStarted(Scene scene)
    {
        stinger.Stop();
        bool menu = scene.name == MenuScenes.Hall || scene.name == MenuScenes.Title || scene.name == MenuScenes.LegacyBook;
        bool arena = !menu && FindAnyObjectByType<MultiplayerManager>() != null;
        var bank = FeelAudio.Bank;

        if (menu)
        {
            // keep the hall theme playing straight through title -> hall
            if (score.theme == ProceduralScore.Hall && !usingClip) { FadeTo(1f, 1f); return; }
            StartTheme(ProceduralScore.Hall, 0, bank != null ? bank.hallMusic : null);
        }
        else if (arena)
        {
            AudioClip clip = null;
            if (bank != null && bank.battleMusic != null && bank.battleMusic.Length > 0)
                clip = bank.battleMusic[Random.Range(0, bank.battleMusic.Length)];
            StartTheme(ProceduralScore.Battle, BattleKeys[battleVisits++ % BattleKeys.Length], clip);
            score.intensity = 0;
        }
        else
        {
            FadeTo(0f, 1f);
        }
    }

    void StartTheme(int theme, int key, AudioClip clip)
    {
        usingClip = clip != null;
        if (usingClip)
        {
            score.Play(ProceduralScore.None, 0);
            if (clipSource.clip != clip || !clipSource.isPlaying) { clipSource.clip = clip; clipSource.Play(); }
        }
        else
        {
            clipSource.Stop();
            score.Play(theme, key);
        }
        fade = 0f;
        FadeTo(1f, theme == ProceduralScore.Battle ? 1.5f : 2.5f);
    }

    void FadeTo(float target, float seconds)
    {
        fadeTarget = target;
        fadeSpeed = 1f / Mathf.Max(0.05f, seconds);
    }

    void Update()
    {
        fade = Mathf.MoveTowards(fade, fadeTarget, fadeSpeed * Time.unscaledDeltaTime);
        var bank = FeelAudio.Bank;
        float vol = bank != null ? bank.musicVolume : 0.5f;
        float duck = FeelAudio.Speaking ? 1f - (bank != null ? bank.musicDuck : 0.45f) : 1f;
        duckLevel = Mathf.MoveTowards(duckLevel, duck, Time.unscaledDeltaTime * 3f);
        float g = vol * fade * duckLevel;
        score.gain = usingClip ? 0f : g;
        clipSource.volume = usingClip ? g : 0f;
        stinger.volume = vol * 1.2f;
    }
    float duckLevel = 1f;

    // ---------- called by MatchDirector ----------

    /// 0 = opening, 1 = after first blood, 2 = final showdown
    public static void SetIntensity(int level)
    {
        if (instance == null) return;
        instance.score.intensity = Mathf.Max(level, level == 0 ? 0 : instance.score.intensity);
    }

    /// The round is won: the battle music drops out under a fanfare
    public static void Victory()
    {
        if (instance == null) return;
        var bank = FeelAudio.Bank;
        instance.Sting(bank != null && bank.victoryStinger != null ? bank.victoryStinger : Fanfare());
    }

    public static void Draw()
    {
        if (instance == null) return;
        instance.Sting(drawSting != null ? drawSting : drawSting = SynthClips.DrawSting());
    }

    static AudioClip fanfare, drawSting;
    static AudioClip Fanfare() => fanfare != null ? fanfare : fanfare = SynthClips.VictoryFanfare();

    void Sting(AudioClip clip)
    {
        FadeTo(0f, 0.35f);
        stinger.Stop();
        stinger.clip = clip;
        stinger.Play();
    }
}
