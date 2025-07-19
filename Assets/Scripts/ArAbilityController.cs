#define INCLUDE_MINI_GRENADE_CLASS
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Hold LT to show a dashed ballistic preview + landing ring. Release to throw a grenade
/// along the exact simulated path (custom gravity supported).
/// Attach this to your gun / muzzle object.
/// </summary>
public class ArAbilityController : MonoBehaviour
{
    [Header("External Refs (Optional)")]
    [Tooltip("Override gamepad; if null uses Gamepad.current.")]
    public Gamepad gamepadOverride;
    [Tooltip("Player root Rigidbody to inherit velocity from (optional).")]
    public Rigidbody playerBody;
    [Tooltip("Grenade prefab with Rigidbody (optional). If unassigned a temp sphere is created.")]
    public GameObject grenadePrefab;
    [Tooltip("Optional separate launch origin.")]
    public Transform muzzle;

    [SerializeField] GameObject dashSegmentPrefab;
    [SerializeField] int segmentPoolSize = 48;

    List<LineRenderer> dashPool = new();
    int activeSegments;
    MaterialPropertyBlock dashMPB;
    public Material ringMaterialBase;


    [Header("Input")]
    public float triggerThreshold = 0.2f;
    public float stickDeadzone = 0.2f;
    public float isoYaw = 45f;          // rotate planar aim for isometric camera

    [Header("Launch & Charge")]
    public bool chargePower = true;
    public float minSpeed = 6f;
    public float maxSpeed = 12f;
    public float chargeTime = 0.75f;
    [Tooltip("Added upward component BEFORE normalization. Higher = steeper arc.")]
    public float upwardBias = 0.55f;
    [Tooltip("Cooldown between throws.")]
    public float fireCooldown = 0.25f;
    [Tooltip("Additive tweak to final initial velocity (e.g., compensate player movement).")]
    public Vector3 launchVelAdjust = Vector3.zero;

    [Header("Physics / Simulation")]
    public Vector3 gravity = new Vector3(0, -11.5f, 0); // Custom gravity (steeper arc)
    public int maxSteps = 80;
    public float timeStep = 0.05f;
    public float maxSimTime = 2.0f;
    public float grenadeRadius = 0.15f;
    public LayerMask collisionMask = ~0;

    [Header("Dash Look")]
    public float segmentLength = 0.22f;
    public float gapLength = 0.14f;
    public float dashWidth = 0.06f;
    public Color dashStartColor = new Color(1f, 0.35f, 0.15f);
    public Color dashEndColor = new Color(0.85f, 0f, 0f);

    [Header("Landing Ring")]
    public bool showLandingRing = false;
    public float landingRingScale = 1.2f;
    public Color landingRingColor = new Color(1f, 0.2f, 0.1f, 0.40f);
    public float landingRingPulseSpeed = 3f;
    public float landingRingPulseAmp = 0.15f;
    public float ringElevation = 0.05f;

    [Header("Quality / Edge Cues")]
    [Tooltip("If first hit is closer than this, show warning colors.")]
    public float tooCloseHitDistance = 1.0f;
    public Color shortArcStartColor = Color.yellow;
    public Color shortArcEndColor = Color.red;

    [Header("Diagnostics")]
    public bool logInit = false;
    public bool guardMaterials = false;     // re-applies correct materials if external scripts overwrite
    public string fxLayerName = "FX";       // optional layer to isolate from global recolor scripts

    // --- Runtime State ---
    Gamepad pad;
    bool aiming;
    float aimStartTime;
    float nextFireAllowed;
    Vector3 cachedVelocity;

    Vector3[] simBuffer;
    int simCount;
    bool simHit;
    RaycastHit simHitInfo;

    // Ring
    GameObject ringGO;
    Mesh ringMesh;
    MeshRenderer ringRenderer;
    Material ringMaterialInstance;
    MaterialPropertyBlock ringMPB;
    float ringBaseScale;
    float ringStartTime;

#if INCLUDE_MINI_GRENADE_CLASS
    [Header("Fallback Mini Grenade")]
    public float grenadeFuseTime = 2.2f;
    public float grenadeBlastRadius = 4f;
    public float grenadeDamage = 50f;
    public float grenadeExplosionForce = 800f;
    public float grenadeUpwardModifier = 0.4f;
    public LayerMask grenadeDamageMask = ~0;
    public GameObject explosionFx;
#endif

    void Awake()
    {
        dashMPB = new MaterialPropertyBlock();
        ringMPB = new MaterialPropertyBlock();

        // Allocate simulation buffer
        simBuffer = new Vector3[Mathf.Max(8, maxSteps + 4)];

        BuildDashPool();
        if (showLandingRing) BuildRing();
    }

    void Update()
    {
        pad = gamepadOverride ?? Gamepad.current;
        if (pad == null) { if (aiming) CancelAiming(); return; }

        float trig = pad.leftTrigger.ReadValue();

        if (!aiming && trig > triggerThreshold && Time.time >= nextFireAllowed)
            StartAiming();
        else if (aiming && trig > triggerThreshold)
            UpdateAiming();
        else if (aiming && trig <= triggerThreshold)
            ReleaseFire();

        if (showLandingRing && ringGO && ringGO.activeSelf)
            UpdateRingVisual();
    }
    // ===== Aiming / Launch =====

    void StartAiming()
    {
        aiming = true;
        aimStartTime = Time.time;
    }

    void CancelAiming()
    {
        aiming = false;
        HideDashes();
        HideRing();
    }

    void UpdateAiming()
    {
        Vector3 startPos = muzzle ? muzzle.position : transform.position;

        // Direction from right stick or forward
        Vector2 rs = pad.rightStick.ReadValue();
        Vector3 dir = (rs.magnitude > stickDeadzone)
            ? Quaternion.Euler(0, isoYaw, 0) * new Vector3(rs.x, 0f, rs.y)
            : transform.forward;
        dir.Normalize();

        // Speed (charging)
        float speed = chargePower
            ? Mathf.Lerp(minSpeed, maxSpeed, Mathf.Clamp01((Time.time - aimStartTime) / chargeTime))
            : Mathf.Lerp(minSpeed, maxSpeed, 0.65f);

        // Build initial velocity
        Vector3 launchDir = (dir + Vector3.up * upwardBias).normalized;
        cachedVelocity = launchDir * speed + launchVelAdjust;

        SimulateArc(startPos, cachedVelocity);

        bool shortArc = simHit && Vector3.Distance(startPos, simHitInfo.point) < tooCloseHitDistance;
        Color cStart = shortArc ? shortArcStartColor : dashStartColor;
        Color cEnd = shortArc ? shortArcEndColor : dashEndColor;

        ShowDashes(cStart, cEnd);
        ShowRing();
    }

    void ReleaseFire()
    {
        aiming = false;
        HideDashes();
        HideRing();
        FireGrenade(cachedVelocity);
    }

    void FireGrenade(Vector3 velocity)
    {
        if (Time.time < nextFireAllowed) return;
        nextFireAllowed = Time.time + fireCooldown;

        GameObject g = grenadePrefab;
#if INCLUDE_MINI_GRENADE_CLASS
        if (!g)
        {
            g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.transform.localScale = Vector3.one * 0.3f;
            Destroy(g.GetComponent<Collider>());
            var sc = g.AddComponent<SphereCollider>();
            sc.radius = 0.15f;

            g.AddComponent<Rigidbody>();
            var mini = g.AddComponent<MiniGrenade>();
            mini.fuseTime = grenadeFuseTime;
            mini.blastRadius = grenadeBlastRadius;
            mini.damage = grenadeDamage;
            mini.explosionForce = grenadeExplosionForce;
            mini.upwardModifier = grenadeUpwardModifier;
            mini.damageMask = grenadeDamageMask;
            mini.explosionFx = explosionFx;
        }
#endif
        Vector3 spawnPos = muzzle ? muzzle.position : transform.position;
        g = Instantiate(g, spawnPos, Quaternion.identity);

        if (g.TryGetComponent<Rigidbody>(out var rb2))
        {
            rb2.useGravity = false; // we'll apply custom gravity
#if UNITY_6000_0_OR_NEWER
            rb2.linearVelocity = velocity + (playerBody ? playerBody.linearVelocity : Vector3.zero);
#else
            rb2.velocity = velocity + (playerBody ? playerBody.velocity : Vector3.zero);
#endif
            var cg = g.AddComponent<CustomGravityBody>();
            cg.gravity = gravity;
        }
#if INCLUDE_MINI_GRENADE_CLASS
        if (g.TryGetComponent<MiniGrenade>(out var mini2))
            mini2.BeginFuse();
#endif
    }

    // ===== Simulation =====

    void SimulateArc(Vector3 startPos, Vector3 initVel)
    {
        if (simBuffer == null || simBuffer.Length < maxSteps + 4)
            simBuffer = new Vector3[maxSteps + 4];

        simCount = 0;
        simHit = false;

        Vector3 pos = startPos;
        Vector3 vel = initVel;
        float t = 0f;

        for (int step = 0; step < maxSteps && t < maxSimTime; step++)
        {
            simBuffer[simCount++] = pos;

            Vector3 newVel = vel + gravity * timeStep;
            Vector3 newPos = pos + vel * timeStep + 0.5f * gravity * (timeStep * timeStep);

            Vector3 seg = newPos - pos;
            float dist = seg.magnitude;

            if (Physics.SphereCast(pos, grenadeRadius, seg.normalized, out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
            {
                if (simCount < simBuffer.Length) simBuffer[simCount++] = hit.point;
                simHit = true;
                simHitInfo = hit;
                break;
            }

            pos = newPos;
            vel = newVel;
            t += timeStep;
        }
    }

    // ===== Dashes =====

    void BuildDashPool()
    {
        if (dashPool.Count > 0) return;
        if (!dashSegmentPrefab)
        {
            Debug.LogError($"[{name}] dashSegmentPrefab not assigned.");
            return;
        }

        // Pre‑instantiate pool
        for (int i = 0; i < segmentPoolSize; i++)
        {
            var inst = Instantiate(dashSegmentPrefab, transform);
            inst.name = $"DashSeg_{i}";
            var lr = inst.GetComponent<LineRenderer>();
            if (!lr)
            {
                Debug.LogError($"[{name}] Prefab missing LineRenderer. Destroying instance.");
                Destroy(inst);
                continue;
            }

            // Ensure we are in pool baseline state
            lr.enabled = false;
            lr.positionCount = 2;
            // (Optional) override width:
            // lr.startWidth = lr.endWidth = dashWidth;
            dashPool.Add(lr);
        }

        if (dashMPB == null)
            dashMPB = new MaterialPropertyBlock();
    }


    void ShowDashes(Color startC, Color endC)
    {
        if (simCount < 2) { HideDashes(); return; }

        float total = 0f;
        var cumulative = new List<float>(simCount);
        cumulative.Add(0f);
        for (int i = 1; i < simCount; i++)
        {
            total += Vector3.Distance(simBuffer[i - 1], simBuffer[i]);
            cumulative.Add(total);
        }

        float cycle = segmentLength + gapLength;
        activeSegments = 0;

        for (float d = 0; d < total && activeSegments < dashPool.Count; d += cycle)
        {
            float segStart = d;
            float segEnd = Mathf.Min(d + segmentLength, total);
            float mid = (segStart + segEnd) * 0.5f;
            float tNorm = total <= 0.0001f ? 0f : mid / total;

            Vector3 p0 = Sample(segStart);
            Vector3 p1 = Sample(segEnd);

            var lr = dashPool[activeSegments++];
            if (!lr.enabled) lr.enabled = true;
            lr.SetPosition(0, p0);
            lr.SetPosition(1, p1);

            Color c = Color.Lerp(startC, endC, tNorm);
            ApplyDashColor(lr, c);
        }

        // Disable unused
        for (int i = activeSegments; i < dashPool.Count; i++)
        {
            if (dashPool[i].enabled)
                dashPool[i].enabled = false;
        }

        Vector3 Sample(float dist)
        {
            int idx = cumulative.BinarySearch(dist);
            if (idx < 0) idx = ~idx;
            idx = Mathf.Clamp(idx, 1, simCount - 1);
            float segLen = cumulative[idx] - cumulative[idx - 1];
            float t = segLen <= 0.0001f ? 0f : (dist - cumulative[idx - 1]) / segLen;
            return Vector3.Lerp(simBuffer[idx - 1], simBuffer[idx], t);
        }
    }


    void HideDashes()
    {
        for (int i = 0; i < activeSegments; i++)
            if (dashPool[i].enabled)
                dashPool[i].enabled = false;
        activeSegments = 0;
    }

    void ApplyDashColor(LineRenderer lr, Color c)
    {
        dashMPB.Clear();
        dashMPB.SetColor("_BaseColor", c);
        dashMPB.SetColor("_Color", c);
        lr.SetPropertyBlock(dashMPB);
    }



    // ===== Ring =====

    void BuildRing()
    {
        if (!showLandingRing) return;
        if (!ringMaterialBase) return;

        ringGO = new GameObject("LandingRing");
        ringGO.transform.SetParent(transform, false);
        int fxLayer = LayerMask.NameToLayer(fxLayerName);
        if (fxLayer >= 0) ringGO.layer = fxLayer;

        ringRenderer = ringGO.AddComponent<MeshRenderer>();
        var mf = ringGO.AddComponent<MeshFilter>();

        ringMesh = GenerateDiscMesh(40);
        mf.sharedMesh = ringMesh;

        // Instance so we can set render queue without altering asset
        ringMaterialInstance = Instantiate(ringMaterialBase);
        ringMaterialInstance.name = ringMaterialBase.name + "_Instance";
        // Force later render (avoid Z-fight); you can adjust 3100 -> 3001 if needed.
        ringMaterialInstance.renderQueue = 3100;
        ringRenderer.sharedMaterial = ringMaterialInstance;

        ringGO.SetActive(false);
        ringBaseScale = landingRingScale;
        ringStartTime = Time.time;
    }

    Mesh GenerateDiscMesh(int segments)
    {
        Mesh m = new Mesh();
        var verts = new Vector3[segments + 1];
        var tris = new int[segments * 3];
        verts[0] = Vector3.zero;
        for (int i = 0; i < segments; i++)
        {
            float ang = i / (float)segments * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.5f;
        }
        for (int i = 0; i < segments; i++)
        {
            int tri = i * 3;
            tris[tri] = 0;
            tris[tri + 1] = i + 1;
            tris[tri + 2] = (i + 1) % segments + 1;
        }
        m.vertices = verts;
        m.triangles = tris;
        m.RecalculateNormals();
        return m;
    }

    void ShowRing()
    {
        if (!showLandingRing || !ringGO) return;
        if (!simHit) { ringGO.SetActive(false); return; }

        ringGO.SetActive(true);
        ringStartTime = Time.time;

        ringGO.transform.position = simHitInfo.point + simHitInfo.normal * ringElevation;
        ringGO.transform.up = simHitInfo.normal;
        ringGO.transform.localScale = Vector3.one * ringBaseScale;
    }

    void HideRing()
    {
        if (ringGO) ringGO.SetActive(false);
    }

    void UpdateRingVisual()
    {
        if (!ringGO || !ringGO.activeSelf) return;

        float t = (Time.time - ringStartTime) * landingRingPulseSpeed;
        float pulse = 1f + Mathf.Sin(t) * landingRingPulseAmp;
        ringGO.transform.localScale = Vector3.one * ringBaseScale * pulse;

        float alphaPulse = 0.85f + 0.15f * Mathf.Sin(t * 0.5f);
        Color c = landingRingColor;
        c.a *= alphaPulse;

        ringMPB.Clear();
        ringMPB.SetColor("_BaseColor", c);
        ringMPB.SetColor("_Color", c);
        ringRenderer.SetPropertyBlock(ringMPB);
    }

#if INCLUDE_MINI_GRENADE_CLASS
    // Minimal internal grenade
    public class MiniGrenade : MonoBehaviour
    {
        [HideInInspector] public float fuseTime;
        [HideInInspector] public float blastRadius;
        [HideInInspector] public float damage;
        [HideInInspector] public float explosionForce;
        [HideInInspector] public float upwardModifier;
        [HideInInspector] public LayerMask damageMask;
        [HideInInspector] public GameObject explosionFx;

        float fuseEnd;
        bool started;

        public void BeginFuse()
        {
            started = true;
            fuseEnd = Time.time + fuseTime;
        }

        void Update()
        {
            if (started && Time.time >= fuseEnd)
                Explode();
        }

        void Explode()
        {
            if (explosionFx) Instantiate(explosionFx, transform.position, Quaternion.identity);
            Collider[] hits = Physics.OverlapSphere(transform.position, blastRadius, damageMask, QueryTriggerInteraction.Ignore);
            foreach (var c in hits)
            {
                if (c.attachedRigidbody)
                    c.attachedRigidbody.AddExplosionForce(explosionForce, transform.position, blastRadius, upwardModifier, ForceMode.Impulse);
            }
            Destroy(gameObject);
        }
    }
#endif

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (simCount > 1 && aiming && simBuffer != null)
        {
            Gizmos.color = Color.red;
            for (int i = 1; i < simCount; i++)
                Gizmos.DrawLine(simBuffer[i - 1], simBuffer[i]);
            if (simHit)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(simHitInfo.point, 0.15f);
            }
        }
    }
#endif
}

/// <summary>
/// Applies per-frame custom gravity (acceleration) to Rigidbody (gravity off).
/// </summary>
public class CustomGravityBody : MonoBehaviour
{
    public Vector3 gravity = new Vector3(0, -11.5f, 0);
    Rigidbody rb;
    void Awake() => rb = GetComponent<Rigidbody>();
    void FixedUpdate()
    {
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity += gravity * Time.fixedDeltaTime;
#else
        rb.velocity += gravity * Time.fixedDeltaTime;
#endif
    }
}
