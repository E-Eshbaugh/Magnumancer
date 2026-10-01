using UnityEngine;
using System.Collections;

/// A glacier slab that erupts from the floor. It can't be shot down: it stands for
/// `lifetime` seconds, slumping and going glassy as it melts, then sinks away.
/// Hits only make it shudder; fire rounds make it melt faster.
public class IceWallEffect : MonoBehaviour
{
    [Header("Size")]
    [Tooltip("Multiplies the model's scale (x = width, y = height, z = thickness)")]
    [SerializeField] Vector3 sizeScale = new Vector3(1.8f, 2f, 1.3f);

    [Header("Eruption")]
    [SerializeField] float riseDuration = 0.22f;
    [Tooltip("How far past full height it springs before settling")]
    [SerializeField] float overshoot = 0.12f;
    [SerializeField] float shakeIntensity = 0.25f;
    [SerializeField] float shakeDuration = 0.3f;
    [Tooltip("Shove for anyone standing where it erupts")]
    [SerializeField] float eruptKnockback = 16f;

    [Header("Melting")]
    [Tooltip("Seconds it stands before it's gone")]
    [SerializeField] float lifetime = 9f;
    [Tooltip("Height left (fraction) just before it sinks away")]
    [Range(0.1f, 1f)] [SerializeField] float meltedHeight = 0.4f;
    [Tooltip("Width left (fraction) just before it sinks away")]
    [Range(0.1f, 1f)] [SerializeField] float meltedWidth = 0.85f;
    [Tooltip("Seconds of melt each point of fire-round damage takes off")]
    [SerializeField] float fireMeltPerDamage = 0.01f;
    [SerializeField] float sinkDuration = 0.5f;

    [SerializeField] GameObject destroyVFX; // optional
    [SerializeField] AudioClip destroySFX; // optional
    [SerializeField] AudioSource audioSource;

    static readonly Color IceBlue = new Color(0.55f, 0.85f, 1f);

    Vector3 fullScale;
    Vector3 originalPosition;
    Bounds meshBounds; // local, unscaled
    float melt;      // 0 = fresh, 1 = melted
    bool risen, sinking;

    // feedback
    Renderer[] renderers;
    MaterialPropertyBlock block;
    Color[] baseColors;
    Coroutine shake;
    float lastHitFx = -1f;

    /// How much of its life is left (0..1)
    public float HealthFraction => 1f - melt;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        block = new MaterialPropertyBlock();
        baseColors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            var m = renderers[i].sharedMaterial;
            baseColors[i] = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor")
                          : m != null && m.HasProperty("_Color") ? m.color : Color.white;
        }

        var mf = GetComponentInChildren<MeshFilter>();
        meshBounds = mf != null && mf.sharedMesh != null ? mf.sharedMesh.bounds
                   : new Bounds(Vector3.up * 1.5f, new Vector3(4f, 3f, 1.5f));

        fullScale = Vector3.Scale(transform.localScale, sizeScale);
        transform.localScale = fullScale;

        originalPosition = transform.position;
        transform.position -= Vector3.up * (meshBounds.max.y * fullScale.y + 0.1f);
    }

    public void BeginRise() => StartCoroutine(Rise());

    /// Melt away now (used when the caster has too many walls up)
    public void BeginMelt() => Shatter();

    IEnumerator Rise()
    {
        Vector3 start = transform.position;
        Vector3 end = originalPosition;

        Erupt();

        for (float t = 0f; t < riseDuration; t += Time.deltaTime)
        {
            // ease-out-back: overshoots the top, then settles
            float k = t / riseDuration - 1f;
            float s = 1.70158f * (overshoot / 0.1f);
            float e = 1f + k * k * ((s + 1f) * k + s);
            transform.position = Vector3.LerpUnclamped(start, end, e);
            yield return null;
        }

        transform.position = end;
        risen = true;
    }

    // The impact: screen shake, rumble, a frost ring, and nobody stuck inside the ice
    void Erupt()
    {
        Vector3 ground = originalPosition;
        Vector3 size = Vector3.Scale(meshBounds.size, fullScale);

        CameraShake.Shake(shakeIntensity, shakeDuration);
        Rumble.Blast(ground, size.x * 1.5f, 0.8f);
        AbilityKit.Shockwave(ground, size.x * 0.6f, IceBlue, 0.45f);

        // Shove anyone caught in the footprint out the nearer face
        Vector3 center = ground + transform.rotation * Vector3.Scale(meshBounds.center, fullScale);
        Vector3 halfExtents = new Vector3(size.x * 0.5f, size.y * 0.5f, size.z * 0.5f + 0.4f);
        var hits = Physics.OverlapBox(center, halfExtents, transform.rotation,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        var shoved = new System.Collections.Generic.HashSet<PlayerMovement3D>();
        foreach (var c in hits)
        {
            var m = c.GetComponentInParent<PlayerMovement3D>();
            if (m == null || !shoved.Add(m)) continue;

            float side = Vector3.Dot(m.transform.position - center, transform.forward);
            m.ApplyKnockback(transform.forward * (side >= 0f ? 1f : -1f) * eruptKnockback);
            Rumble.Play(m.gamepad, 0.6f, 0.4f, 0.25f);
        }
    }

    void Update()
    {
        if (!risen || sinking) return;

        melt += Time.deltaTime / Mathf.Max(0.1f, lifetime);
        ApplyMelt();
        if (melt >= 1f) StartCoroutine(Sink());
    }

    // Slumps slowly at first, then faster as it gives way
    void ApplyMelt()
    {
        float m = Mathf.Clamp01(melt);
        float slump = m * m;
        transform.localScale = new Vector3(
            fullScale.x * Mathf.Lerp(1f, meltedWidth, slump),
            fullScale.y * Mathf.Lerp(1f, meltedHeight, slump),
            fullScale.z * Mathf.Lerp(1f, meltedWidth, slump));

        // wetter and glassier as it goes
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Color c = Color.Lerp(baseColors[i], baseColors[i] * new Color(0.7f, 0.85f, 1.1f, 1f), m);
            c.a = baseColors[i].a * Mathf.Lerp(1f, 0.55f, m);
            renderers[i].GetPropertyBlock(block);
            block.SetColor("_BaseColor", c);
            block.SetColor("_Color", c);
            renderers[i].SetPropertyBlock(block);
        }
    }

    IEnumerator Sink()
    {
        sinking = true;
        if (shake != null) { StopCoroutine(shake); shake = null; }

        if (destroyVFX)
            Instantiate(destroyVFX, transform.position, Quaternion.identity);
        if (destroySFX && audioSource)
            audioSource.PlayOneShot(destroySFX);

        Vector3 start = transform.position;
        Vector3 end = start - Vector3.up * (meshBounds.max.y * transform.localScale.y + 0.1f);
        for (float t = 0f; t < sinkDuration; t += Time.deltaTime)
        {
            float k = t / sinkDuration;
            transform.position = Vector3.Lerp(start, end, k * k);
            yield return null;
        }
        Destroy(gameObject);
    }

    /// Unbreakable: hits only jolt it
    public void TakeDamage(int amount)
    {
        if (!risen || sinking || amount <= 0) return;
        Jolt();
    }

    /// Fire rounds eat into the time it has left
    public void Scorch(int damage)
    {
        if (sinking || damage <= 0) return;
        melt = Mathf.Min(1f, melt + damage * fireMeltPerDamage / Mathf.Max(0.1f, lifetime));
        if (risen) Jolt();
    }

    /// Melt away right now
    public void Shatter()
    {
        if (sinking) return;
        StopAllCoroutines();
        shake = null;
        StartCoroutine(Sink());
    }

    void Jolt()
    {
        if (Time.time - lastHitFx < 0.05f) return; // shotgun volleys: one jolt, not twelve
        lastHitFx = Time.time;
        if (shake != null) StopCoroutine(shake);
        shake = StartCoroutine(Shudder());
    }

    // A quick jolt when hit (doesn't drift: always returns to where it stood)
    IEnumerator Shudder()
    {
        Vector3 home = originalPosition;
        for (float t = 0; t < 0.12f; t += Time.deltaTime)
        {
            transform.position = home + Random.insideUnitSphere * 0.05f * (1f - t / 0.12f);
            yield return null;
        }
        transform.position = home;
        shake = null;
    }
}
