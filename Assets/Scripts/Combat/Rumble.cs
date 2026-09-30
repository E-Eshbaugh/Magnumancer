using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Controller vibration. Nothing else should call Gamepad.SetMotorSpeeds: effects
/// request rumble here and a single runner mixes them per controller each frame
/// (the strongest pulse wins, sustained buzzes add on top). That way overlapping
/// effects never cut each other off (an old "stop rumble" timer used to silence
/// an explosion that landed mid-recoil), pausing silences everything, and all the
/// feel tuning lives in the presets at the bottom of this file.
/// low = the heavy (left) motor, high = the light, buzzy (right) motor; both 0..1.
/// </summary>
public static class Rumble
{
    /// Master strength (0 = off). Hook up to a settings menu later.
    public static float Strength = 1f;

    struct Pulse
    {
        public float low, high, start, duration;
        public bool fade;
    }

    class PadState
    {
        public readonly List<Pulse> pulses = new();
        public readonly Dictionary<string, Vector2> sustained = new();
        public float sentLow = -1f, sentHigh = -1f;
    }

    static readonly Dictionary<Gamepad, PadState> pads = new();

    // ---------- Core API ----------

    /// A burst that (by default) fades out over `duration` seconds.
    public static void Play(Gamepad pad, float low, float high, float duration, bool fade = true)
    {
        if (pad == null || duration <= 0f || (low <= 0f && high <= 0f)) return;
        EnsureRunner();
        WarnIfUnsupported(pad);
        State(pad).pulses.Add(new Pulse
        {
            low = Mathf.Clamp01(low), high = Mathf.Clamp01(high),
            start = Time.time, duration = duration, fade = fade
        });
    }

    public static void Play(GameObject player, float low, float high, float duration, bool fade = true)
        => Play(PadOf(player), low, high, duration, fade);

    /// A buzz that lasts until cleared (overclock hum, standing in lava). Same key replaces.
    public static void Hold(Gamepad pad, string key, float low, float high)
    {
        if (pad == null) return;
        EnsureRunner();
        State(pad).sustained[key] = new Vector2(Mathf.Clamp01(low), Mathf.Clamp01(high));
    }

    public static void Release(Gamepad pad, string key)
    {
        if (pad != null && pads.TryGetValue(pad, out var s)) s.sustained.Remove(key);
    }

    /// Everyone near a blast feels it, fading with distance.
    public static void Blast(Vector3 center, float radius, float strength = 1f)
    {
        if (radius <= 0f) return;
        foreach (var m in Object.FindObjectsByType<PlayerMovement3D>())
        {
            if (m.gamepad == null) continue;
            float d = Vector3.Distance(center, m.transform.position);
            if (d > radius) continue;
            float k = strength * (1f - d / radius);
            Play(m.gamepad, 0.9f * k, 0.6f * k, 0.25f + 0.25f * k);
        }
    }

    public static void StopAll()
    {
        foreach (var kv in pads)
        {
            kv.Value.pulses.Clear();
            kv.Value.sustained.Clear();
        }
        foreach (var pad in Gamepad.all) pad.SetMotorSpeeds(0f, 0f);
        foreach (var s in pads.Values) s.sentLow = s.sentHigh = 0f;
    }

    /// The controller of the player this object belongs to (null for monsters etc.)
    public static Gamepad PadOf(GameObject go)
    {
        if (go == null) return null;
        var m = go.GetComponentInParent<PlayerMovement3D>();
        return m != null ? m.gamepad : null;
    }

    // ---------- Presets (tune feel here) ----------

    /// Gun kick; recoil is the weapon's recoil stat (after Stonebind etc.)
    public static void Fire(Gamepad pad, float recoil)
    {
        float r = Mathf.Clamp01(recoil);
        Play(pad, 0.1f + r * 0.6f, 0.25f + r * 0.6f, 0.08f + r * 0.1f);
    }

    public static void Hurt(GameObject victim, float fractionOfMaxHealth)
    {
        float k = Mathf.Clamp01(0.25f + fractionOfMaxHealth * 2.5f);
        Play(victim, 0.7f * k, 0.5f * k, 0.12f + 0.15f * k);
    }

    public static void LifeLost(GameObject victim) => Play(victim, 1f, 0.8f, 0.6f);

    /// Your hit landed (very light so an SMG doesn't turn into a massage chair)
    public static void HitConfirm(GameObject attacker) => Play(attacker, 0f, 0.18f, 0.05f, fade: false);

    /// You took someone's life
    public static void KillConfirm(GameObject attacker) => Play(attacker, 0.45f, 0.9f, 0.3f);

    public static void Spawn(GameObject player) => Play(player, 0.5f, 1f, 0.3f);

    public static void Dash(Gamepad pad) => Play(pad, 0.15f, 0.35f, 0.1f);

    public static void Land(Gamepad pad, float impact) => Play(pad, 0.4f * impact, 0.15f * impact, 0.1f);

    public static void Swap(Gamepad pad) => Play(pad, 0.05f, 0.25f, 0.06f, fade: false);

    public static void ShellLoaded(Gamepad pad) => Play(pad, 0f, 0.2f, 0.04f, fade: false);

    public static void ReloadDone(Gamepad pad) => Play(pad, 0.25f, 0.4f, 0.08f, fade: false);

    public static void WizardAbility(GameObject caster) => Play(caster, 0.45f, 0.7f, 0.25f);

    public static void GunAbility(Gamepad pad) => Play(pad, 0.3f, 0.6f, 0.18f);

    public static void Stunned(GameObject victim, float duration) => Play(victim, 0.2f, 0.7f, Mathf.Max(0.15f, duration));

    // ---------- Runner ----------

    // Unity's Input System can't drive every controller's motors. Most notably Xbox
    // controllers on macOS (they show up as "Controller"): SetMotorSpeeds silently does
    // nothing. Say so once instead of leaving it a mystery. (Works: Xbox on Windows,
    // PS4 DualShock on Mac/Windows over USB or Bluetooth, PS5 DualSense over USB.)
    static readonly HashSet<Gamepad> warned = new();
    static void WarnIfUnsupported(Gamepad pad)
    {
        if (!warned.Add(pad)) return;
        bool mac = Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer;
        if (mac && pad is UnityEngine.InputSystem.XInput.XInputController)
            Debug.LogWarning($"[Rumble] '{pad.displayName}' is an Xbox controller on macOS — Unity's Input System " +
                             "can't vibrate it there. Rumble works with Xbox on Windows or a PS4/PS5 pad on Mac.");
    }

    static PadState State(Gamepad pad)
    {
        if (!pads.TryGetValue(pad, out var s)) pads[pad] = s = new PadState();
        return s;
    }

    static RumbleRunner runner;

    static void EnsureRunner()
    {
        if (runner != null) return;
        var go = new GameObject("[Rumble]") { hideFlags = HideFlags.HideInHierarchy };
        Object.DontDestroyOnLoad(go);
        runner = go.AddComponent<RumbleRunner>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        pads.Clear();
        warned.Clear();
        runner = null;
        Strength = 1f;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot() => EnsureRunner(); // so combat events are heard from the first hit

    internal static void Tick()
    {
        float now = Time.time;
        bool silent = GamePause.IsPaused || Strength <= 0f;

        foreach (var kv in pads)
        {
            var pad = kv.Key;
            var s = kv.Value;
            if (pad == null || !pad.added) continue;

            float low = 0f, high = 0f;
            for (int i = s.pulses.Count - 1; i >= 0; i--)
            {
                var p = s.pulses[i];
                float t = (now - p.start) / p.duration;
                if (t >= 1f) { s.pulses.RemoveAt(i); continue; }
                float k = p.fade ? 1f - t : 1f;
                low = Mathf.Max(low, p.low * k);
                high = Mathf.Max(high, p.high * k);
            }
            foreach (var v in s.sustained.Values)
            {
                low += v.x;
                high += v.y;
            }

            if (silent) low = high = 0f;
            low = Mathf.Clamp01(low * Strength);
            high = Mathf.Clamp01(high * Strength);

            // only talk to the device when something changed
            if (Mathf.Abs(low - s.sentLow) > 0.01f || Mathf.Abs(high - s.sentHigh) > 0.01f)
            {
                pad.SetMotorSpeeds(low, high);
                s.sentLow = low;
                s.sentHigh = high;
            }
        }
    }

    internal static void OnSceneChanged() => StopAll();

    // Combat feedback comes straight from the damage hooks, so every weapon,
    // ability and hazard gets it without knowing about rumble.
    internal static void OnDamaged(GameObject victim, GameObject attacker, float amount)
    {
        if (amount <= 0f || victim == null) return;

        var health = victim.GetComponent<PlayerHealthControl>();
        if (health != null)
            Hurt(victim, amount / Mathf.Max(1f, health.maxHealth));

        if (attacker != null && attacker != victim)
            HitConfirm(attacker);
    }

    internal static void OnKilled(GameObject victim, GameObject attacker, Vector3 position)
    {
        if (victim != null && victim.GetComponent<PlayerHealthControl>() != null)
            LifeLost(victim);
        if (attacker != null && attacker != victim)
            KillConfirm(attacker);
    }
}

[AddComponentMenu("")]
class RumbleRunner : MonoBehaviour
{
    void OnEnable()
    {
        DamageEvents.Damaged += Rumble.OnDamaged;
        DamageEvents.Killed += Rumble.OnKilled;
        SceneManager.activeSceneChanged += SceneChanged;
    }

    void OnDisable()
    {
        DamageEvents.Damaged -= Rumble.OnDamaged;
        DamageEvents.Killed -= Rumble.OnKilled;
        SceneManager.activeSceneChanged -= SceneChanged;
        Rumble.StopAll();
    }

    void SceneChanged(Scene a, Scene b) => Rumble.OnSceneChanged();

    void LateUpdate() => Rumble.Tick();

    void OnApplicationPause(bool paused) { if (paused) Rumble.StopAll(); }

    void OnApplicationQuit() => Rumble.StopAll();
}
