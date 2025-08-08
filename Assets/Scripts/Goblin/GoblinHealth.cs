using UnityEngine;
using UnityEngine.UI;

public class GoblinHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth = 100f;

    private RectTransform fillTransform;

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

    public void TakeDamage(float damage)
    {
        currentHealth = Mathf.Clamp(currentHealth - damage, 0f, maxHealth);
        UpdateHealthBar();

        if (currentHealth <= 0f)
        {
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
