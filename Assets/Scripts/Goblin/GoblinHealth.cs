using UnityEngine;
using UnityEngine.UI;

public class GoblinHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    private RectTransform fillTransform;
    private bool isDead;

    void Start()
    {
        currentHealth = maxHealth;

        // Find the health bar's fill image under this object
        Transform healthBar = transform.Find("HealthBarCanvas/Fill");
        if (healthBar != null)
        {
            fillTransform = healthBar.GetComponent<RectTransform>();
        }
        else
        {
            Debug.LogWarning($"[GoblinHealth] Could not find health bar Fill under {name}");
        }
    }

    /// attacker may be null (environment / unknown source)
    public void TakeDamage(float damage, GameObject attacker = null)
    {
        if (isDead || damage <= 0f) return;

        damage = DamageEvents.ModifyOutgoing(attacker, damage);
        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
        UpdateHealthBar();
        DamageEvents.RaiseDamaged(gameObject, attacker, damage);

        if (currentHealth <= 0f)
        {
            isDead = true;
            DamageEvents.RaiseKilled(gameObject, attacker, transform.position);

            // Soulfracture: a void-marked monster explodes when it dies
            var fx = GetComponent<StatusEffects>();
            if (fx != null && fx.VoidMarkedBy != null &&
                fx.VoidMarkedBy.TryGetComponent<CursedPlayer>(out var hollow))
                hollow.Explode(transform.position, gameObject, fx.VoidMarkedBy);

            GetComponent<GoblinAnimationControl>()?.OnDeath();
        }
    }

    void UpdateHealthBar()
    {
        if (fillTransform != null)
        {
            float healthPercent = currentHealth / maxHealth;
            fillTransform.localScale = new Vector3(healthPercent, 1f, 1f);
        }
    }
}
