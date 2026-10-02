using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Makes a hit land: the victim flashes white, a punch sound scaled to the damage, a burst
/// of sparks in the attacker's color, a little camera shake, and on a chunky hit a split
/// second of hitstop (the whole game freezes for a few frames, like Smash). Lethal hits
/// skip the hitstop so the elimination's own slow-mo plays. A kill gets a bright chime.
/// Damage-over-time ticks stay quiet: no hitstop, and flashes/sounds are rate limited.
/// Listens to DamageEvents; nothing else needs to call it.
/// </summary>
[AddComponentMenu("")]
public class HitFeedback : MonoBehaviour
{
    // Hitstop: hits below MinStopFrac of max health don't freeze; the biggest ones freeze longest
    const float MinStopFrac = 0.06f;
    const float StopMin = 0.035f, StopMax = 0.1f, StopCap = 0.13f;
    const float StopScale = 0.04f;
    const float StopCooldown = 0.12f;   // real seconds between freezes, so a brawl doesn't stutter

    const float FlashTime = 0.07f;
    const float HurtGap = 0.07f;        // per victim
    const float HeavyFrac = 0.2f;

    static HitFeedback runner;
    static Material flashMat;

    class Flash { public Renderer[] renderers; public Material[][] saved; public float until; }
    readonly Dictionary<GameObject, Flash> flashes = new();
    readonly List<GameObject> done = new();
    readonly Dictionary<GameObject, float> lastHurt = new();

    float stopUntil, stopStarted, stopCooldownUntil, ownScale = -1f;
    float lastMonsterHurt;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (runner != null) return;
        var go = new GameObject("[HitFeedback]");
        DontDestroyOnLoad(go);
        runner = go.AddComponent<HitFeedback>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { runner = null; flashMat = null; }

    void OnEnable()
    {
        DamageEvents.Damaged += OnDamaged;
        DamageEvents.Killed += OnKilled;
        SceneManager.activeSceneChanged += SceneChanged;
    }

    void OnDisable()
    {
        DamageEvents.Damaged -= OnDamaged;
        DamageEvents.Killed -= OnKilled;
        SceneManager.activeSceneChanged -= SceneChanged;
        EndHitstop();
    }

    void SceneChanged(Scene a, Scene b)
    {
        EndHitstop();
        flashes.Clear();
        lastHurt.Clear();
    }

    void OnDamaged(GameObject victim, GameObject attacker, float amount)
    {
        if (victim == null || amount <= 0f) return;

        var player = victim.GetComponent<PlayerHealthControl>();
        if (player != null) { PlayerHit(player, attacker, amount); return; }

        // Monsters: flash and a light thud, no freeze (the horde is a lot of hits)
        if (victim.GetComponent<GoblinHealth>() != null)
        {
            StartFlash(victim, FlashTime * 0.8f);
            var bank = FeelAudio.Bank;
            if (bank != null && Time.unscaledTime - lastMonsterHurt > 0.06f && IsPlayer(attacker))
            {
                lastMonsterHurt = Time.unscaledTime;
                FeelAudio.PlayRandom(bank.hurt, bank.hurtVolume * 0.45f, Random.Range(1.05f, 1.25f));
            }
        }
    }

    void PlayerHit(PlayerHealthControl victim, GameObject attacker, float amount)
    {
        // Players raise Damaged before their health drops, so this is the health they had
        bool lethal = amount >= victim.currentHealth;
        float frac = Mathf.Clamp01(amount / Mathf.Max(1f, victim.maxHealth));
        float now = Time.unscaledTime;
        var go = victim.gameObject;

        StartFlash(go, FlashTime);

        // Sound: per-victim gap so poison/lava ticks and shotgun pellets don't machine-gun
        var bank = FeelAudio.Bank;
        if (bank != null && (!lastHurt.TryGetValue(go, out float last) || now - last > HurtGap))
        {
            lastHurt[go] = now;
            bool heavy = frac >= HeavyFrac || lethal;
            float vol = bank.hurtVolume * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(frac / 0.25f));
            if (heavy && bank.heavyHurt != null && bank.heavyHurt.Length > 0)
                FeelAudio.PlayRandom(bank.heavyHurt, vol, Random.Range(0.85f, 0.97f));
            else
                FeelAudio.PlayRandom(bank.hurt, vol, Random.Range(0.92f, 1.1f));
        }

        // Unowned hazards and ticks: just the flash and the sound
        if (attacker == null || attacker == go) return;

        Color c = GlowLine.Brighten(WizardShade.Of(attacker));
        PowerFx.Sparks(AbilityKit.Chest(go), c, Mathf.RoundToInt(Mathf.Lerp(5f, 16f, frac / 0.3f)),
            Mathf.Lerp(4f, 8f, frac / 0.3f), 0.3f);

        if (lethal) return;   // the elimination brings its own shake and slow-mo

        if (frac >= 0.03f)
            CameraShake.Shake(Mathf.Lerp(0.05f, 0.22f, frac / 0.3f), Mathf.Lerp(0.1f, 0.2f, frac / 0.3f));
        if (frac >= MinStopFrac)
            Hitstop(Mathf.Lerp(StopMin, StopMax, (frac - MinStopFrac) / 0.3f));
    }

    void OnKilled(GameObject victim, GameObject attacker, Vector3 at)
    {
        if (victim == null || victim.GetComponent<PlayerHealthControl>() == null) return;
        // let WizardDeathEffect's slow-mo start from normal time
        EndHitstop();
        if (attacker != null && attacker != victim && IsPlayer(attacker))
            FeelAudio.KillChime();
    }

    static bool IsPlayer(GameObject go) => go != null && go.GetComponentInParent<PlayerHealthControl>() != null;

    // ---------- Hitstop ----------

    void Hitstop(float seconds)
    {
        if (GamePause.IsPaused) return;
        float now = Time.unscaledTime;
        if (ownScale > 0f)
        {
            // already frozen: a bigger hit can stretch it, up to the cap
            stopUntil = Mathf.Min(Mathf.Max(stopUntil, now + seconds), stopStarted + StopCap);
            return;
        }
        if (now < stopCooldownUntil || !Mathf.Approximately(Time.timeScale, 1f)) return;
        Time.timeScale = ownScale = StopScale;
        stopStarted = now;
        stopUntil = now + seconds;
    }

    void EndHitstop()
    {
        if (ownScale <= 0f) return;
        // never un-pause the game or undo someone else's slow-mo
        if (!GamePause.IsPaused && Mathf.Approximately(Time.timeScale, ownScale)) Time.timeScale = 1f;
        ownScale = -1f;
        stopCooldownUntil = Time.unscaledTime + StopCooldown;
    }

    // ---------- Flash ----------

    void StartFlash(GameObject victim, float seconds)
    {
        float until = Time.unscaledTime + seconds;
        if (flashes.TryGetValue(victim, out var f)) { f.until = Mathf.Max(f.until, until); return; }

        var mat = FlashMaterial();
        if (mat == null) return;
        var picked = new List<Renderer>();
        foreach (var r in victim.GetComponentsInChildren<Renderer>())
            if (Flashable(r)) picked.Add(r);
        if (picked.Count == 0) return;

        f = new Flash { renderers = picked.ToArray(), saved = new Material[picked.Count][], until = until };
        for (int i = 0; i < picked.Count; i++)
        {
            var mats = picked[i].sharedMaterials;
            f.saved[i] = mats;
            var white = new Material[mats.Length];
            for (int m = 0; m < white.Length; m++) white[m] = mat;
            picked[i].sharedMaterials = white;
        }
        flashes[victim] = f;
    }

    // Solid body parts only: no particles, trails, glow rings, text or see-through shields
    static bool Flashable(Renderer r)
    {
        if (!r.enabled || !(r is MeshRenderer || r is SkinnedMeshRenderer)) return false;
        if (r.GetComponent<TMPro.TMP_Text>() != null) return false;
        var mats = r.sharedMaterials;
        if (mats.Length == 0) return false;
        foreach (var m in mats)
            if (m == null || m.renderQueue >= 2450) return false;
        return true;
    }

    void Update()
    {
        float now = Time.unscaledTime;
        if (ownScale > 0f && (now >= stopUntil || GamePause.IsPaused)) EndHitstop();

        if (flashes.Count == 0) return;
        done.Clear();
        foreach (var kv in flashes)
            if (kv.Key == null || now >= kv.Value.until) done.Add(kv.Key);
        foreach (var key in done)
        {
            if (flashes.TryGetValue(key, out var f)) Restore(f);
            flashes.Remove(key);
        }
    }

    static void Restore(Flash f)
    {
        for (int i = 0; i < f.renderers.Length; i++)
        {
            var r = f.renderers[i];
            // skip anything someone else re-skinned mid-flash (their materials win)
            if (r == null) continue;
            var now = r.sharedMaterials;
            if (now.Length == 0 || now[0] != flashMat) continue;
            r.sharedMaterials = f.saved[i];
        }
    }

    void OnDestroy()
    {
        foreach (var f in flashes.Values) Restore(f);
        flashes.Clear();
    }

    static Material FlashMaterial()
    {
        if (flashMat != null) return flashMat;
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) return null;
        flashMat = new Material(shader) { name = "HitFlash" };
        var c = new Color(1f, 0.97f, 0.92f, 1f);
        if (flashMat.HasProperty("_BaseColor")) flashMat.SetColor("_BaseColor", c);
        if (flashMat.HasProperty("_Color")) flashMat.SetColor("_Color", c);
        return flashMat;
    }
}
