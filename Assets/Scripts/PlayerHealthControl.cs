using UnityEngine;
using System;

[RequireComponent(typeof(CharacterController))]
public class PlayerHealthControl : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 100;

    // Initialize inline so it’s never left at zero.
    public int currentHealth { get; private set; } = 100;

    public event Action<int, int> OnHealthChanged;
    public event Action OnDeath;

    [Header("UI")]
    [Tooltip("Crest UI controller for the health mask")]
    public CrestUIController healthMask;

    void Awake()
    {
        // Force currentHealth to max, in case it was overridden
        currentHealth = maxHealth;
        Debug.Log($"[Health] Awake(): currentHealth = {currentHealth}/{maxHealth}");
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Called by the Bullet on hit.
    /// </summary>
    public void TakeDamage(int amount)
    {
        Debug.Log($"[Health] TakeDamage( {amount} ) called; before = {currentHealth}/{maxHealth}");

        if (amount <= 0)
            return;

        currentHealth = Mathf.Max(currentHealth - amount, 0);

        Debug.Log($"[Health] After damage: currentHealth = {currentHealth}/{maxHealth}");

        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth == 0)
            Die();
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;

        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        Debug.Log($"[Health] Healed: currentHealth = {currentHealth}/{maxHealth}");
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private void UpdateUI()
    {
        if (healthMask != null)
        {
            float frac = (float)currentHealth / maxHealth;
            healthMask.SetHealthFraction(frac);
        }
    }

    private void Die()
    {
        Debug.Log($"[Health] {name} has died.");
        OnDeath?.Invoke();
        // TODO: disable input, play death animation, respawn, etc.
    }
}
