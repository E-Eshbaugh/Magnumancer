using UnityEngine;
using System;

[RequireComponent(typeof(CharacterController))]
public class PlayerHealthControl : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100f;

    // Initialize inline so it’s never left at zero.
    public float currentHealth { get; private set; } = 100f;

    public event Action<float, float> OnHealthChanged;
    public event Action OnDeath;
    public bool invincible = false;

    [Header("UI")]
    [Tooltip("Crest UI controller for the health mask")]
    public CrestUIController healthMask;
    public GameObject[] stock;
    public int stockCount = 3;

    void Awake()
    {
        // Force currentHealth to max, in case it was overridden
        currentHealth = maxHealth;
        Debug.Log($"[Health] Awake(): currentHealth = {currentHealth}/{maxHealth}");
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        for (int i = 0; i < stock.Length; i++)
        {
            if (i < stockCount)
            {
                stock[i].SetActive(true);
            }
            else
            {
                stock[i].SetActive(false);
            }
        }
    }

    /// <summary>
    /// Called by the Bullet on hit.
    /// </summary>
    public void TakeDamage(float amount)
    {
        Debug.Log($"[Health] {tag} TakeDamage( {amount} ) called; before = {currentHealth}/{maxHealth}");

        if (amount <= 0 || invincible)
            return;

        currentHealth = Mathf.Max(currentHealth - amount, 0);

        Debug.Log($"[Health] {tag} After damage: currentHealth = {currentHealth}/{maxHealth}");

        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth == 0 && stockCount > 0)
            StockDamage();
        else if (currentHealth <= 0)
            Die();
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || currentHealth <= 0)
        {
            Debug.Log($"[Health] Heal blocked: amount={amount}, currentHealth={currentHealth}");
            return;
        }

        float before = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
        float healed = currentHealth - before;

        if (healed > 0)
        {
            Debug.Log($"[Health] {gameObject.name} healed +{healed}, now at {currentHealth}/{maxHealth}");
            UpdateUI();
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }
        else
        {
            Debug.Log($"[Health] Heal had no effect — already at max.");
        }
    }



    private void UpdateUI()
    {
        if (healthMask != null)
        {
            float frac = (float)currentHealth / maxHealth;
            healthMask.SetHealthFraction(frac);
        }
    }

    // deactivate the player 
    private void Die()
    {
        Debug.Log("[Health] Player has died.");
        OnDeath?.Invoke();
        // Optionally, you can disable the player character or trigger a respawn
        gameObject.SetActive(false);
        // Additional logic for death, like playing an animation or sound, can be added here.
    }

    private void StockDamage()
    {
        Debug.Log($"[Health] Stock damage taken. Remaining stock: {stockCount - 1}");
        stockCount--;
        if (stockCount < 0)
            stockCount = 0;

        // Update the stock UI
        for (int i = 0; i < stock.Length; i++)
        {
            if (i < stockCount)
            {
                stock[i].SetActive(true);
            }
            else
            {
                stock[i].SetActive(false);
            }
        }

        // Reset health to max after taking stock damage
        currentHealth = maxHealth;
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        //0.5 second invincibility after stock damage
        invincible = true;
        Invoke(nameof(ResetInvincibility), 0.1f);
    }

    private void ResetInvincibility()
    {
        invincible = false;
        Debug.Log("[Health] Invincibility reset.");
    }
}
