using UnityEngine;

public class GoblinHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    private bool isDead;
    public bool IsDead => isDead;

    void Awake() => currentHealth = maxHealth;

    // Soul Swap trades percentages, including with bosses, without killing either target.
    public void SetHealthFraction(float fraction)
    {
        if (!isDead) currentHealth = Mathf.Clamp(Mathf.Round(maxHealth * Mathf.Clamp01(fraction)), 1f, maxHealth);
    }

    void Start()
    {
        // No health bars: the prefab's floating bar is switched off
        var bar = transform.Find("HealthBarCanvas");
        if (bar != null) bar.gameObject.SetActive(false);
    }

    /// attacker may be null (environment / unknown source)
    public void TakeDamage(float damage, GameObject attacker = null)
    {
        if (!Teams.CanHarm(gameObject, attacker)) return;
        if (isDead || damage <= 0f) return;

        damage = DamageEvents.ModifyOutgoing(attacker, damage);
        damage = Mathf.Min(currentHealth, Mathf.Max(0f, damage));
        if (damage <= 0f) return;
        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
        // Passives can deal more damage inside Damaged. Claim a lethal hit before
        // callbacks so a brand burst/Cold Precision can't award this death twice.
        bool lethal = currentHealth <= 0f;
        if (lethal) isDead = true;
        DamageEvents.RaiseDamaged(gameObject, attacker, damage);

        if (lethal)
        {
            isDead = true;
            DamageEvents.RaiseKilled(gameObject, attacker, transform.position);

            // Soulfracture: a void-marked monster explodes when it dies
            var fx = GetComponent<StatusEffects>();
            if (fx != null && fx.VoidMarkedBy != null &&
                fx.VoidMarkedBy.TryGetComponent<CursedPlayer>(out var hollow))
                hollow.Explode(transform.position, gameObject, fx.VoidMarkedBy);

            var animation = GetComponent<GoblinAnimationControl>();
            if (animation != null) animation.OnDeath();
            else Destroy(gameObject);
        }
    }
}
