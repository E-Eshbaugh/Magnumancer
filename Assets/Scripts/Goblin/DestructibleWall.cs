using UnityEngine;
using UnityEngine.Events;

public class DestructibleWall : MonoBehaviour
{
    [Header("Wall Settings")]
    public float maxHealth = 25000f;
    public float currentHealth;

    [Tooltip("UI bar that resizes an Image based on this wall's health")]
    public SingleBarHealthUI singleBarUI;

    [Header("VFX & SFX")]
    public GameObject breakVFX;
    public AudioClip breakSFX;
    public AudioSource audioSource; // can be on same GameObject

    [Header("Camera Shake Settings")]
    [Tooltip("Max shake intensity per hit at huge damage values")]
    public float maxShakeIntensityPerHit = 0.25f;
    [Tooltip("Damage amount that produces max shake")]
    public float damageForMaxShake = 500f;
    [Tooltip("Duration of shake per hit")]
    public float shakeDurationPerHit = 0.15f;

    [Header("Break Shake Settings")]
    public float breakShakeIntensity = 0.35f;
    public float breakShakeDuration = 0.45f;

    [Header("Events")]
    public UnityEvent onWallDestroyed;

    private bool isDestroyed = false;

    /// Normalized health [0..1] for UI
    public float CurrentHealth01 => Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));

    void OnEnable()
    {
        isDestroyed = false;
        currentHealth = maxHealth;
        PushToUI(); // if bar is visible at start, show full (SingleBarHealthUI also starts full)
    }

    void Start()
    {
        // Auto-find if not wired in Inspector
        if (singleBarUI == null)
        {
            singleBarUI = FindFirstObjectByType<SingleBarHealthUI>();
        }
        PushToUI();
    }

    /// Call this from weapons/abilities on hit
    public void TakeDamage(float amount)
    {
        if (isDestroyed || amount <= 0f) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        PushToUI();

        // Scaled shake based on damage dealt
        float shakeAmount = Mathf.Clamp01(amount / Mathf.Max(1f, damageForMaxShake)) * maxShakeIntensityPerHit;
        if (shakeAmount > 0f)
            CameraShake.Shake(shakeAmount, shakeDurationPerHit);

        if (currentHealth <= 0f)
            DestroyWall();
    }

    private void PushToUI()
    {
        // Show THIS wall's normalized health on the single bar
        singleBarUI?.ReflectWall(this);
    }

    private void DestroyWall()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // Bigger shake for final break
        CameraShake.Shake(breakShakeIntensity, breakShakeDuration);

        // Play SFX
        if (audioSource != null && breakSFX != null)
            audioSource.PlayOneShot(breakSFX);

        // Spawn VFX
        if (breakVFX != null)
            Instantiate(breakVFX, transform.position, Quaternion.identity);

        // Trigger custom events (open door, spawn enemies, etc.)
        onWallDestroyed?.Invoke();

        // Hide / disable wall
        gameObject.SetActive(false);
    }

    /// Optional: Heal or repair the wall.
    public void Heal(float amount)
    {
        if (isDestroyed || amount <= 0f) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        PushToUI();
    }
}
