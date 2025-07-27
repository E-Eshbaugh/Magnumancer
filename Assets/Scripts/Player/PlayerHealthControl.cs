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
    public void TakeDamage(int amount)
    {
        Debug.Log($"[Health] TakeDamage( {amount} ) called; before = {currentHealth}/{maxHealth}");

        if (amount <= 0)
            return;

        currentHealth = Mathf.Max(currentHealth - amount, 0);

        Debug.Log($"[Health] After damage: currentHealth = {currentHealth}/{maxHealth}");

        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth == 0 && stockCount > 0)
            StockDamage();
        else if (currentHealth <= 0)
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
    }
}
