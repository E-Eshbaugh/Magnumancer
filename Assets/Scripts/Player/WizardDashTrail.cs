using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dashing leaves the wizard's spawn lightning behind them: a bolt in their theme color
/// traces the dash path as it happens, hangs crackling for a beat, then fades.
/// </summary>
[RequireComponent(typeof(PlayerMovement3D))]
public class WizardDashTrail : MonoBehaviour
{
    [Header("Timing")]
    [Tooltip("The bolt hangs (still crackling) this long after the dash ends")]
    public float holdTime = 0.12f;
    public float fadeTime = 0.3f;
    public float crackleInterval = 0.04f;

    [Header("Look")]
    public float height = 0.9f;        // above the feet, roughly hip height
    public float width = 0.28f;
    public float coreWidth = 0.08f;
    public float jitter = 0.35f;
    [Tooltip("Bolt points per unit of dash distance")]
    public float pointsPerUnit = 3f;
    [Tooltip("Thin side-branches per bolt")]
    public int forks = 2;
    public float intensity = 3f;

    PlayerMovement3D movement;
    Color color = Color.white;
    static Material sharedMat;

    public static WizardDashTrail AddTo(GameObject player, WizardData wizard)
    {
        if (player == null) return null;
        var trail = player.GetComponent<WizardDashTrail>();
        if (trail == null) trail = player.AddComponent<WizardDashTrail>();
        trail.color = GlowLine.Brighten(WizardShade.Of(player));
        return trail;
    }

    void Awake() => movement = GetComponent<PlayerMovement3D>();
    void OnEnable() { if (movement) movement.OnDash += HandleDash; }
    void OnDisable() { if (movement) movement.OnDash -= HandleDash; }

    void HandleDash()
    {
        // Runs on its own object so the trail finishes even if the player dies mid-dash
        var host = new GameObject($"DashTrail ({name})");
        var runner = host.AddComponent<TrailRunner>();
        runner.StartCoroutine(Run(host, runner));
    }

    IEnumerator Run(GameObject host, TrailRunner runner)
    {
        if (sharedMat == null) sharedMat = GlowLine.CreateMaterial(intensity);

        var path = new List<Vector3> { Anchor() };
        var glow = GlowLine.Make(host.transform, "Bolt", 2, width, sharedMat);
        var core = GlowLine.Make(host.transform, "BoltCore", 2, coreWidth, sharedMat);
        var forkLines = new List<LineRenderer>();
        for (int i = 0; i < forks; i++)
            forkLines.Add(GlowLine.Make(host.transform, "Fork", 4, width * 0.35f, sharedMat));

        Color coreColor = Color.Lerp(color, Color.white, 0.7f);
        float dashTime = movement != null ? movement.dashDuration : 0.2f;
        float t = 0f, nextCrackle = 0f;

        // 1) Follow the dash, drawing the bolt behind the wizard as they go
        while (t < dashTime && this != null && isActiveAndEnabled)
        {
            t += Time.deltaTime;
            Vector3 p = Anchor();
            if ((p - path[path.Count - 1]).sqrMagnitude > 0.04f) path.Add(p);
            if (Time.time >= nextCrackle)
            {
                Shape(glow, core, forkLines, path);
                nextCrackle = Time.time + crackleInterval;
            }
            Tint(glow, core, forkLines, coreColor, 1f);
            yield return null;
        }

        // 2) Hang for a beat, still crackling, then 3) fade out
        float hold = 0f;
        while (hold < holdTime + fadeTime)
        {
            hold += Time.deltaTime;
            float a = hold <= holdTime ? 1f : 1f - (hold - holdTime) / fadeTime;
            if (Time.time >= nextCrackle && a > 0.3f)
            {
                Shape(glow, core, forkLines, path);
                nextCrackle = Time.time + crackleInterval * 1.5f;
            }
            Tint(glow, core, forkLines, coreColor, a);
            glow.widthMultiplier = Mathf.Lerp(0.5f, 1f, a);
            yield return null;
        }

        Destroy(host);
    }

    Vector3 Anchor() => transform.position + Vector3.up * height;

    void Shape(LineRenderer glow, LineRenderer core, List<LineRenderer> forkLines, List<Vector3> path)
    {
        if (path.Count < 2)
        {
            glow.enabled = core.enabled = false;
            foreach (var f in forkLines) f.enabled = false;
            return;
        }

        // resample the dash path into evenly spaced, jittered points
        float length = 0f;
        for (int i = 1; i < path.Count; i++) length += Vector3.Distance(path[i - 1], path[i]);
        int n = Mathf.Clamp(Mathf.CeilToInt(length * pointsPerUnit) + 1, 3, 64);
        glow.positionCount = core.positionCount = n;

        for (int i = 0; i < n; i++)
        {
            float f = i / (float)(n - 1);
            Vector3 p = Sample(path, f * length);
            if (i > 0 && i < n - 1)
                p += new Vector3(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter) * 0.6f, Random.Range(-jitter, jitter));
            glow.SetPosition(i, p);
            core.SetPosition(i, p);
        }
        glow.enabled = core.enabled = true;

        // little branches snapping off the main bolt
        foreach (var fork in forkLines)
        {
            int at = Random.Range(1, n - 1);
            Vector3 from = glow.GetPosition(at);
            Vector3 dir = new Vector3(Random.Range(-1f, 1f), Random.Range(-0.6f, 0.3f), Random.Range(-1f, 1f)).normalized;
            GlowLine.Bolt(fork, from, from + dir * Random.Range(0.4f, 0.9f), jitter * 0.5f);
            fork.enabled = true;
        }
    }

    static Vector3 Sample(List<Vector3> path, float dist)
    {
        for (int i = 1; i < path.Count; i++)
        {
            float seg = Vector3.Distance(path[i - 1], path[i]);
            if (dist <= seg || i == path.Count - 1)
                return Vector3.Lerp(path[i - 1], path[i], seg > 1e-4f ? Mathf.Clamp01(dist / seg) : 0f);
            dist -= seg;
        }
        return path[path.Count - 1];
    }

    void Tint(LineRenderer glow, LineRenderer core, List<LineRenderer> forkLines, Color coreColor, float alpha)
    {
        GlowLine.SetColor(glow, color, alpha * 0.85f);
        GlowLine.SetColor(core, coreColor, alpha);
        foreach (var f in forkLines) GlowLine.SetColor(f, color, alpha * 0.7f);
    }

    [AddComponentMenu("")]
    class TrailRunner : MonoBehaviour { }
}
