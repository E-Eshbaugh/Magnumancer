using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rifles (laserRange > 0) — hold LT to scope in:
///  • a laser in your wizard's color traces the exact path your next bullet takes
///  • no spread, but you move a little slower
///  • Focus: stay scoped for focusTime and your next shot hits harder. When it's
///    ready the laser flares and throbs and the controller pulses in time with it;
///    the ability bar shows the charge.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class LaserScope : MonoBehaviour
{
    [Tooltip("Where the laser originates (defaults to the gun's FireController3D firePoint)")]
    public Transform firePoint;

    [Tooltip("Layers the laser can hit (default Everything)")]
    public LayerMask hitLayers = ~0;

    [Header("Controller")]
    public Gamepad gamepad;   // assigned via MultiplayerManager

    [Header("Appearance")]
    [Tooltip("Unused: the laser builds its own additive glow material")]
    public Material mat;
    [Tooltip("Colored glow around the beam")]
    public float laserWidth = 0.2f;
    [Tooltip("Bright center of the beam")]
    public float coreWidth = 0.055f;
    [Tooltip("HDR brightness (above 1 blooms)")]
    public float glowIntensity = 2.5f;
    [Tooltip("Used when the wizard has no theme color")]
    public Color fallbackColor = Color.red;

    [Header("Focus Ready Pulse")]
    [Tooltip("Throb speed (radians/sec; 9 ≈ 1.4 throbs a second)")]
    public float pulseSpeed = 9f;
    [Tooltip("Beam width multiplier at the peak of a throb")]
    public float pulseWidthBoost = 1.8f;
    [Tooltip("Beam brightness multiplier at the peak of a throb")]
    public float pulseIntensityBoost = 2.4f;

    [Header("Scoped")]
    [Tooltip("Move speed while scoped")]
    [Range(0.5f, 1f)] public float scopedMoveMultiplier = 0.85f;

    [Header("Focus Shot")]
    [Tooltip("Seconds scoped before the next shot is focused")]
    public float focusTime = 1f;
    public float focusDamageMultiplier = 1.5f;

    private LineRenderer lr;
    private AmmoControl ammoControl;
    private FireController3D fire;
    private PlayerMovement3D movement;
    private WeaponAbilityControl abilityBar;
    private GameObject ownerRoot;
    private Material glowMat, coreMat;
    private LineRenderer core;
    private Color laserColor;
    private bool wasFocusReady;

    private bool scoped;
    private float scopedSince;
    private readonly RaycastHit[] hits = new RaycastHit[16];

    /// Frostwarden's sniper synergy charges Focus faster
    [HideInInspector] public float focusSpeedMultiplier = 1f;
    float FocusTime => focusTime / Mathf.Max(0.05f, focusSpeedMultiplier);

    bool FocusReady => scoped && Time.time - scopedSince >= FocusTime;

    public void Setup(Gamepad pad)
    {
        gamepad = pad;
        ApplyColor();
    }

    void Awake()
    {
        lr = GetComponent<LineRenderer>();
        ammoControl = GetComponentInParent<AmmoControl>();
        fire = GetComponent<FireController3D>();
        movement = GetComponentInParent<PlayerMovement3D>();
        ownerRoot = movement != null ? movement.gameObject : null;
        if (fire != null && fire.firePoint != null) firePoint = fire.firePoint;

        glowMat = GlowLine.CreateMaterial(glowIntensity);
        coreMat = GlowLine.CreateMaterial(glowIntensity);
        GlowLine.Configure(lr, 2, laserWidth, glowMat);
        lr.enabled = false;
        core = GlowLine.Make(transform, "LaserCore", 2, coreWidth, coreMat);
        core.enabled = false;
    }

    void Start()
    {
        abilityBar = WeaponAbilityControl.FindFor(this);
        if (ammoControl != null) ammoControl.ShotDamageModifier += FocusShot;
        ApplyColor();
    }

    void OnDestroy()
    {
        if (ammoControl != null) ammoControl.ShotDamageModifier -= FocusShot;
        if (glowMat != null) Destroy(glowMat);
        if (coreMat != null) Destroy(coreMat);
    }

    void ApplyColor()
    {
        var wiz = ammoControl != null ? ammoControl.wizard : null;
        Color theme = wiz != null && wiz.themeColor.maxColorComponent > 0.01f && wiz.themeColor != Color.white
            ? wiz.themeColor
            : fallbackColor;
        // dark hues (Hollow purple, Granite brown) are pushed bright so the beam reads
        laserColor = GlowLine.Brighten(theme);

        if (lr != null) lr.colorGradient = BeamGradient(laserColor, 0.9f, 0.35f);
        if (core != null) core.colorGradient = BeamGradient(GlowLine.Core(theme), 1f, 0.5f);
    }

    static Gradient BeamGradient(Color c, float startAlpha, float endAlpha) => new Gradient
    {
        mode = GradientMode.Blend,
        colorKeys = new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
        alphaKeys = new[] { new GradientAlphaKey(startAlpha, 0f), new GradientAlphaKey(endAlpha, 1f) }
    };

    void Update()
    {
        float laserRange = ammoControl != null && ammoControl.currentGun != null ? ammoControl.currentGun.laserRange : 0f;
        bool wantScope = gamepad != null && firePoint != null && laserRange > 0f
            && !GamePause.InputBlocked && gamepad.leftTrigger.ReadValue() > 0.1f;

        SetScoped(wantScope);
        if (!scoped)
        {
            lr.enabled = false;
            core.enabled = false;
            SetFocusFeedback(false, 0f);
            return;
        }

        // Same origin/direction as FireController3D.Shoot, so the laser is where the bullet goes
        Vector3 origin = firePoint.position;
        Vector3 dir = fire != null ? fire.ShotDirection() : firePoint.up;
        Vector3 end = origin + dir * laserRange;
        if (RaycastIgnoringSelf(origin, dir, laserRange, out var hit))
            end = hit.point;

        lr.SetPosition(0, origin);
        lr.SetPosition(1, end);
        core.SetPosition(0, origin);
        core.SetPosition(1, end);

        // Focus ready: the beam throbs wide and bright (0..1 wave, sharp peaks)
        bool ready = FocusReady;
        float wave = ready ? Mathf.Pow(0.5f + 0.5f * Mathf.Sin((Time.time - scopedSince) * pulseSpeed), 2f) : 0f;
        float widthK = ready ? Mathf.Lerp(1.15f, pulseWidthBoost, wave) : 1f;
        float glowK = ready ? Mathf.Lerp(1.3f, pulseIntensityBoost, wave) : 1f;

        lr.startWidth = laserWidth * widthK;
        lr.endWidth = laserWidth * widthK * 0.7f;
        core.startWidth = coreWidth * widthK;
        core.endWidth = coreWidth * widthK * 0.7f;
        GlowLine.SetIntensity(glowMat, glowIntensity * glowK);
        GlowLine.SetIntensity(coreMat, glowIntensity * glowK);
        lr.enabled = true;
        core.enabled = true;

        SetFocusFeedback(ready, wave);

        abilityBar?.ReportFill((Time.time - scopedSince) / Mathf.Max(0.01f, FocusTime));
    }

    void SetScoped(bool on)
    {
        if (on == scoped) return;
        scoped = on;
        scopedSince = Time.time;
        if (ammoControl != null) ammoControl.spreadMultiplier = on ? 0f : 1f;
        if (movement != null)
        {
            if (on) movement.SetSpeedModifier("scope", scopedMoveMultiplier);
            else movement.ClearSpeedModifier("scope");
        }
    }

    // Controller: a kick the moment Focus is ready, then a throb in time with the beam
    void SetFocusFeedback(bool ready, float wave)
    {
        if (ready && !wasFocusReady)
            Rumble.Play(gamepad, 0.35f, 0.8f, 0.15f);

        if (ready) Rumble.Hold(gamepad, "focus", 0.08f * wave, 0.05f + 0.3f * wave);
        else if (wasFocusReady) Rumble.Release(gamepad, "focus");

        wasFocusReady = ready;
    }

    int FocusShot(int damage)
    {
        if (!FocusReady) return damage;
        scopedSince = Time.time; // spend the focus; re-aim to charge again
        return Mathf.RoundToInt(damage * focusDamageMultiplier);
    }

    bool RaycastIgnoringSelf(Vector3 origin, Vector3 dir, float range, out RaycastHit best)
    {
        best = default;
        int n = Physics.RaycastNonAlloc(origin, dir, hits, range,
            hitLayers & Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        for (int i = 0; i < n; i++)
        {
            var h = hits[i];
            if (h.distance >= bestDist) continue;
            if (ownerRoot != null && h.collider.transform.IsChildOf(ownerRoot.transform)) continue;
            if (h.collider.GetComponentInParent<Bullet>() != null) continue; // bullets don't block bullets
            best = h;
            bestDist = h.distance;
        }
        return bestDist < float.MaxValue;
    }

    void OnDisable()
    {
        SetScoped(false);
        SetFocusFeedback(false, 0f);
        if (lr) lr.enabled = false;
        if (core) core.enabled = false;
    }
}
