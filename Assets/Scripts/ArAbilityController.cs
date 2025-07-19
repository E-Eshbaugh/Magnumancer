#define INCLUDE_MINI_GRENADE_CLASS
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

/// <summary>
/// Attach to your gunPlaceHolder (muzzle). Creates dashed trajectory + landing ring,
/// simulates arc while holding LT, fires grenade on release.
/// No external prefabs needed for dashes/ring (only the grenade prefab).
/// </summary>
public class ArAbilityController : MonoBehaviour
{
    [Header("External Refs (Optional)")]
    [Tooltip("If null, will use Gamepad.current. In multiplayer assign each player their own pad.")]
    public Gamepad gamepadOverride;
    [Tooltip("Optional: Player root Rigidbody to inherit velocity from.")]
    public Rigidbody playerBody;
    [Tooltip("Grenade prefab with Rigidbody. If left null AND INCLUDE_MINI_GRENADE_CLASS is defined, a basic sphere grenade will be created procedurally.")]
    public GameObject grenadePrefab;

    [Header("Input")]
    public float triggerThreshold = 0.2f;  // LT press threshold
    public float stickDeadzone    = 0.2f;  // Right stick
    public float isoYaw           = 45f;   // Rotate planar aim (adjust to match camera yaw)

    [Header("Launch & Charge")]
    public bool  chargePower      = true;
    public float minSpeed         = 8f;
    public float maxSpeed         = 22f;
    public float chargeTime       = 1.0f;
    public float upwardBias       = 2.0f; // Adds arc height
    public float fireCooldown     = 0.25f;

    [Header("Physics / Simulation")]
    public Vector3 gravity        = Physics.gravity;
    public int   maxSteps         = 80;
    public float timeStep         = 0.05f;
    public float maxSimTime       = 1.0f;
    public float grenadeRadius    = 0.15f;
    public LayerMask collisionMask = ~0; // everything by default

    [Header("Dash Look")]
    public int   segmentPoolSize  = 48;
    public float segmentLength    = 0.22f;
    public float gapLength        = 0.14f;
    public float dashWidth        = 0.06f;
    public Color dashStartColor   = new Color(1f, 0.35f, 0.15f);
    public Color dashEndColor     = new Color(0.85f, 0f, 0f);

    [Header("Landing Ring")]
    public bool  showLandingRing  = true;
    public float landingRingScale = 1.2f;
    public Color landingRingColor = new Color(1f, 0.2f, 0.1f, 0.40f);
    public float landingRingPulseSpeed = 3f;
    public float landingRingPulseAmp   = 0.15f;

    [Header("Quality / Edge Cues")]
    public float tooCloseHitDistance = 1.0f; // if collision occurs before this distance, recolor dashes (warn)
    public Color shortArcStartColor  = Color.yellow;
    public Color shortArcEndColor    = Color.red;

    // --- State ---
    Gamepad pad;
    bool aiming;
    float aimStartTime;
    float nextFireAllowed;
    Vector3 cachedVelocity;

    Vector3[] simBuffer;
    int simCount;
    bool simHit;
    RaycastHit simHitInfo;

    // Dashes
    List<LineRenderer> dashPool = new();
    int activeSegments;
    Material dashSharedMaterial;
    MaterialPropertyBlock mpb;

    // Landing ring
    GameObject ringGO;
    Mesh ringMesh;
    MeshRenderer ringRenderer;
    Material ringMaterial;
    float ringBaseScale;
    float ringStartTime;

#if INCLUDE_MINI_GRENADE_CLASS
    // Minimal inner grenade fallback
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
        simBuffer = new Vector3[maxSteps + 2];
        mpb = new MaterialPropertyBlock();
        BuildDashPool();
        if (showLandingRing) BuildRing();
    }

    void Update()
    {
        pad = gamepadOverride != null ? gamepadOverride : Gamepad.current;
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
        // Aim direction
        Vector2 rs = pad.rightStick.ReadValue();
        Vector3 dir;
        if (rs.magnitude > stickDeadzone)
        {
            Quaternion iso = Quaternion.Euler(0, isoYaw, 0);
            dir = iso * new Vector3(rs.x, 0f, rs.y);
        }
        else
        {
            dir = transform.forward;
        }
        dir.Normalize();

        // Speed
        float speed;
        if (chargePower)
        {
            float c = Mathf.Clamp01((Time.time - aimStartTime) / chargeTime);
            speed = Mathf.Lerp(minSpeed, maxSpeed, c);
        }
        else
        {
            speed = Mathf.Lerp(minSpeed, maxSpeed, 0.65f); // middle bias if no charge
        }

        Vector3 launchDir = (dir + Vector3.up * upwardBias).normalized;
        cachedVelocity = launchDir * speed;

        // Simulate
        SimulateArc(transform.position, cachedVelocity);

        // Dynamic recolor if arc is "too short"
        bool shortArc = simHit && Vector3.Distance(transform.position, simHitInfo.point) < tooCloseHitDistance;
        var cStart = shortArc ? shortArcStartColor : dashStartColor;
        var cEnd   = shortArc ? shortArcEndColor   : dashEndColor;

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
            // Build minimal sphere grenade programmatically
            g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            g.transform.localScale = Vector3.one * 0.3f;
            Destroy(g.GetComponent<Collider>()); // remove default collider
            var sc = g.AddComponent<SphereCollider>();
            sc.radius = 0.15f;

            var rb = g.AddComponent<Rigidbody>();
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
        g = Instantiate(g, transform.position, Quaternion.identity);
        if (g.TryGetComponent<Rigidbody>(out var rb2))
            rb2.linearVelocity = velocity + (playerBody ? playerBody.linearVelocity : Vector3.zero);

#if INCLUDE_MINI_GRENADE_CLASS
        if (g.TryGetComponent<MiniGrenade>(out var mini2))
            mini2.BeginFuse();
#endif
    }

    // --- Simulation ---
    void SimulateArc(Vector3 startPos, Vector3 initVel)
    {
        simCount = 0;
        simHit = false;

        Vector3 pos = startPos;
        Vector3 vel = initVel;
        float t = 0f;

        for (int step = 0; step < maxSteps && t < maxSimTime; step++)
        {
            simBuffer[simCount++] = pos;

            Vector3 newVel = vel + gravity * timeStep;
            Vector3 newPos = pos + vel * timeStep + 0.5f * gravity * timeStep * timeStep;

            Vector3 seg = newPos - pos;
            float dist = seg.magnitude;
            if (Physics.SphereCast(pos, grenadeRadius, seg.normalized, out RaycastHit hit, dist, collisionMask, QueryTriggerInteraction.Ignore))
            {
                simBuffer[simCount++] = hit.point;
                simHit = true;
                simHitInfo = hit;
                break;
            }

            pos = newPos;
            vel = newVel;
            t += timeStep;
        }
    }

    // --- Dashes ---
    void BuildDashPool()
    {
        dashSharedMaterial = new Material(Shader.Find("Unlit/Color"));
        dashSharedMaterial.color = Color.white;

        for (int i = 0; i < segmentPoolSize; i++)
        {
            var go = new GameObject("DashSeg_" + i);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = lr.endWidth = dashWidth;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.loop = false;
            lr.numCapVertices = 0;
            lr.numCornerVertices = 0;
            lr.material = dashSharedMaterial;
            go.SetActive(false);
            dashPool.Add(lr);
        }
    }

    void ShowDashes(Color startC, Color endC)
    {
        if (simCount < 2) { HideDashes(); return; }

        // cumulative length
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
            if (!lr.gameObject.activeSelf) lr.gameObject.SetActive(true);
            lr.SetPosition(0, p0);
            lr.SetPosition(1, p1);

            // gradient via mpb
            Color c = Color.Lerp(startC, endC, tNorm);
            mpb.SetColor("_Color", c);
            lr.SetPropertyBlock(mpb);
        }
        // deactivate rest
        for (int i = activeSegments; i < dashPool.Count; i++)
            if (dashPool[i].gameObject.activeSelf) dashPool[i].gameObject.SetActive(false);

        // local sample
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
            if (dashPool[i].gameObject.activeSelf)
                dashPool[i].gameObject.SetActive(false);
        activeSegments = 0;
    }

    // --- Landing Ring ---
    void BuildRing()
    {
        ringGO = new GameObject("LandingRing");
        ringGO.transform.SetParent(transform, false);
        ringRenderer = ringGO.AddComponent<MeshRenderer>();
        var mf = ringGO.AddComponent<MeshFilter>();
        ringMaterial = new Material(Shader.Find("Unlit/Color"));
        ringMaterial.color = landingRingColor;
        ringRenderer.sharedMaterial = ringMaterial;
        ringMesh = GenerateDiscMesh(24);
        mf.sharedMesh = ringMesh;
        ringGO.SetActive(false);
        ringBaseScale = landingRingScale;
        ringStartTime = Time.time;
    }

    Mesh GenerateDiscMesh(int segments)
    {
        Mesh m = new Mesh();
        Vector3[] verts = new Vector3[segments + 1];
        int[] tris = new int[segments * 3];
        verts[0] = Vector3.zero;

        for (int i = 0; i < segments; i++)
        {
            float ang = (float)i / segments * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 0.5f; // radius 0.5
        }
        for (int i = 0; i < segments; i++)
        {
            int a = 0;
            int b = i + 1;
            int c = (i + 1) % segments + 1;
            int tri = i * 3;
            tris[tri] = a; tris[tri + 1] = b; tris[tri + 2] = c;
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
        ringGO.transform.position = simHitInfo.point + simHitInfo.normal * 0.01f;
        ringGO.transform.up = simHitInfo.normal;
        ringGO.transform.localScale = Vector3.one * ringBaseScale;
    }

    void HideRing()
    {
        if (ringGO) ringGO.SetActive(false);
    }

    void UpdateRingVisual()
    {
        float t = (Time.time - ringStartTime) * landingRingPulseSpeed;
        float pulse = 1f + Mathf.Sin(t) * landingRingPulseAmp;
        ringGO.transform.localScale = Vector3.one * ringBaseScale * pulse;

        // Slight alpha pulse
        float alphaPulse = 0.75f + 0.25f * Mathf.Sin(t * 0.5f);
        var c = landingRingColor;
        c.a *= alphaPulse;
        mpb.SetColor("_Color", c);
        ringRenderer.SetPropertyBlock(mpb);
    }

#if INCLUDE_MINI_GRENADE_CLASS
    // --- Minimal internal grenade (optional) ---
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
                // c.GetComponent<Health>()?.Apply(damage);
                if (c.attachedRigidbody)
                    c.attachedRigidbody.AddExplosionForce(explosionForce, transform.position, blastRadius, upwardModifier, ForceMode.Impulse);
            }
            Destroy(gameObject);
        }
    }
#endif

    // --- Debug Gizmos ---
#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (simCount > 1 && aiming)
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
