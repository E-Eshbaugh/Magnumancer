using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The wizard arriving: a bolt in their theme color strikes from the sky, the wizard
/// appears where it lands with a flash, a shock ring and their element's particle
/// burst (WizardData.spawnEffectPrefab). Played at round start and on every respawn.
/// </summary>
public class WizardSpawnEffect : MonoBehaviour
{
    [Header("Timing")]
    public float preStrikeDelay = 0.1f;   // wizard hidden, bolt about to fall
    public float boltFallTime = 0.12f;    // tip travels sky -> ground
    public float boltLingerTime = 0.3f;   // flickers after landing, then fades
    public float ringTime = 0.5f;

    [Header("Look")]
    public float boltHeight = 16f;
    public float boltWidth = 0.35f;
    public float boltJitter = 0.55f;
    public int boltSegments = 14;
    public float ringRadius = 2.6f;
    public float flashIntensity = 9f;
    public float flashRange = 7f;

    static Material lineMaterial;
    const float GlowIntensity = 3f;

    Transform target;
    WizardData wizard;
    Color color;
    readonly List<Renderer> hidden = new();
    Transform hiddenTarget;

    // Wizards currently invisible, waiting for their bolt (health bars hide meanwhile)
    static readonly HashSet<Transform> arriving = new();
    public static bool IsArriving(Transform player) => player != null && arriving.Contains(player);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => arriving.Clear();

    /// Plays the arrival on `player`. delay lets several players arrive in a quick sequence.
    public static WizardSpawnEffect Play(GameObject player, WizardData wizard, float delay = 0f)
    {
        if (player == null) return null;
        var host = new GameObject($"SpawnEffect ({player.name})");
        host.transform.position = player.transform.position;
        var fx = host.AddComponent<WizardSpawnEffect>();
        fx.target = player.transform;
        fx.wizard = wizard;
        fx.color = GlowLine.Brighten(player != null ? WizardShade.Of(player) : ThemeColorOf(wizard));
        fx.StartCoroutine(fx.Run(delay));
        return fx;
    }

    public static Color ThemeColorOf(WizardData wizard)
    {
        Color c = wizard != null ? wizard.themeColor : Color.white;
        c.a = 1f;
        return c.maxColorComponent > 0.01f ? c : Color.white;
    }

    IEnumerator Run(float delay)
    {
        // Wizard vanishes until the bolt hits
        HideWizard();

        yield return new WaitForSeconds(delay + preStrikeDelay);
        if (target == null) { Destroy(gameObject); yield break; }

        Vector3 ground = target.position;
        transform.position = ground;

        var bolt = MakeLine("Bolt", boltSegments + 1, boltWidth);
        var core = MakeLine("BoltCore", boltSegments + 1, boltWidth * 0.35f);
        SetLineColor(bolt, color, 1f);
        SetLineColor(core, Color.Lerp(color, Color.white, 0.75f), 1f);

        // 1) Bolt falls
        float t = 0f;
        while (t < boltFallTime)
        {
            t += Time.deltaTime;
            float reach = Mathf.Clamp01(t / boltFallTime);
            ShapeBolt(bolt, core, ground, reach);
            yield return null;
        }

        // 2) Impact: wizard appears with a flash, ring and element burst
        RevealWizard();
        CameraShake.Shake(0.12f, 0.15f);
        Rumble.Spawn(target.gameObject);
        var flash = MakeFlash(ground);
        var ring = MakeLine("Ring", 41, 0.18f);
        ring.loop = true;

        if (wizard != null && wizard.spawnEffectPrefab != null)
        {
            var burst = Instantiate(wizard.spawnEffectPrefab, ground + Vector3.up * 0.2f, Quaternion.identity);
            Destroy(burst, 4f);
        }

        // 3) Bolt flickers out while the ring expands
        float total = Mathf.Max(boltLingerTime, ringTime);
        float flicker = 0f;
        t = 0f;
        while (t < total)
        {
            t += Time.deltaTime;
            flicker -= Time.deltaTime;

            float boltFade = 1f - Mathf.Clamp01(t / boltLingerTime);
            if (flicker <= 0f && boltFade > 0f)
            {
                ShapeBolt(bolt, core, ground, 1f); // re-jag for a crackling look
                flicker = 0.045f;
            }
            SetLineColor(bolt, color, boltFade);
            SetLineColor(core, Color.Lerp(color, Color.white, 0.75f), boltFade);
            bolt.widthMultiplier = Mathf.Lerp(0.4f, 1f, boltFade);

            float r = Mathf.Clamp01(t / ringTime);
            float eased = 1f - (1f - r) * (1f - r);
            ShapeRing(ring, ground + Vector3.up * 0.08f, Mathf.Lerp(0.3f, ringRadius, eased));
            SetLineColor(ring, color, 1f - r);
            ring.widthMultiplier = Mathf.Lerp(1.4f, 0.3f, r);

            if (flash != null)
                flash.intensity = flashIntensity * (1f - Mathf.Clamp01(t / (total * 0.8f)));

            yield return null;
        }

        Destroy(gameObject);
    }

    // ---------- Wizard visibility ----------

    void HideWizard()
    {
        hidden.Clear();
        if (target == null) return;
        hiddenTarget = target;
        arriving.Add(target);
        foreach (var r in target.GetComponentsInChildren<Renderer>())
        {
            // body and gun meshes only: aim lines/lasers manage themselves
            if (!(r is MeshRenderer || r is SkinnedMeshRenderer)) continue;
            if (!r.enabled) continue;
            r.enabled = false;
            hidden.Add(r);
        }
    }

    void RevealWizard()
    {
        foreach (var r in hidden)
            if (r != null) r.enabled = true;
        hidden.Clear();
        if (hiddenTarget != null) arriving.Remove(hiddenTarget);
        hiddenTarget = null;
    }

    // Never leave a wizard invisible if the effect is cut short (scene change, player removed)
    void OnDestroy() => RevealWizard();

    // ---------- Shapes ----------

    void ShapeBolt(LineRenderer bolt, LineRenderer core, Vector3 ground, float reach)
    {
        Vector3 top = ground + Vector3.up * boltHeight;
        Vector3 tip = Vector3.Lerp(top, ground, reach);
        int n = bolt.positionCount;
        Vector3 side = Camera.main != null ? Camera.main.transform.right : Vector3.right;
        Vector3 depth = Vector3.Cross(side, Vector3.up);

        for (int i = 0; i < n; i++)
        {
            float f = i / (float)(n - 1);
            Vector3 p = Vector3.Lerp(top, tip, f);
            // jagged in the middle, pinned at both ends
            if (i > 0 && i < n - 1)
            {
                float j = boltJitter * Mathf.Sin(f * Mathf.PI);
                p += side * Random.Range(-j, j) + depth * Random.Range(-j, j) * 0.5f;
            }
            bolt.SetPosition(i, p);
            core.SetPosition(i, p);
        }
    }

    static void ShapeRing(LineRenderer ring, Vector3 center, float radius)
    {
        int n = ring.positionCount;
        for (int i = 0; i < n; i++)
        {
            float a = i / (float)n * Mathf.PI * 2f;
            ring.SetPosition(i, center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius);
        }
    }

    // ---------- Builders ----------

    LineRenderer MakeLine(string lineName, int points, float width)
        => GlowLine.Make(transform, lineName, points, width, LineMaterial());

    static void SetLineColor(LineRenderer lr, Color c, float alpha) => GlowLine.SetColor(lr, c, alpha);

    Light MakeFlash(Vector3 ground)
    {
        var go = new GameObject("Flash");
        go.transform.SetParent(transform, false);
        go.transform.position = ground + Vector3.up * 1.2f;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = flashRange;
        light.intensity = flashIntensity;
        light.shadows = LightShadows.None;
        return light;
    }

    // shared by every spawn effect (they never animate its brightness)
    static Material LineMaterial()
    {
        if (lineMaterial == null) lineMaterial = GlowLine.CreateMaterial(GlowIntensity);
        return lineMaterial;
    }
}
