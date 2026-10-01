using UnityEngine;

public class GoblinHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    private bool isDead;

    void Start()
    {
        currentHealth = maxHealth;

        // No health bars: the prefab's floating bar is switched off
        var bar = transform.Find("HealthBarCanvas");
        if (bar != null) bar.gameObject.SetActive(false);
    }

    /// attacker may be null (environment / unknown source)
    public void TakeDamage(float damage, GameObject attacker = null)
    {
        if (isDead || damage <= 0f) return;

        damage = DamageEvents.ModifyOutgoing(attacker, damage);
        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
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
}
