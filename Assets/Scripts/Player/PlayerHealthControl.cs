using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;

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
    private bool isDead = false;
    public CursedPlayer cursedPlayer;

    [Header("UI")]
    [Tooltip("Crest UI controller for the health mask")]
    public CrestUIController healthMask;
    [Tooltip("Heart icons; extra ones are cloned at runtime if a wizard has more lives")]
    public GameObject[] stock;
    [Tooltip("Extra lives after the current one (set from the wizard's hearts at match start)")]
    public int stockCount = 3;
    public PlayerMovement3D movement;

    /// Time.time of the last hit that actually did damage (Verdant Resurgence)
    public float LastDamageTime { get; private set; } = -999f;
    public bool IsDead => isDead;

    float invulnerableUntil;
    GameObject lastAttacker;

    // heart icon layout, captured from the icons placed in the scene
    readonly List<GameObject> stockSlots = new();
    Vector2 stockCenter, stockSpacing = new Vector2(10f, 0f);

    void Awake()
    {
        movement = GetComponent<PlayerMovement3D>();
        currentHealth = maxHealth;
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (cursedPlayer == null)
            cursedPlayer = GetComponent<CursedPlayer>();

        CacheStockLayout();
        UpdateStockUI();
    }

    /// <summary>
    /// Sets total lives from the wizard's heart count (1 heart = no extra lives).
    /// </summary>
    public void SetLives(int hearts)
    {
        stockCount = Mathf.Max(0, hearts - 1);
        UpdateStockUI();
    }

    /// Ignore all damage for a while (Last Rites).
    public void GrantInvulnerability(float seconds)
        => invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);

    /// <summary>
    /// Called by bullets, explosions, hazards and abilities. attacker may be null.
    /// </summary>
    public void TakeDamage(float amount, GameObject attacker = null)
    {
        // isDead: lingering hazards (lava, poison) can still tick on a dead player
        if (amount <= 0 || invincible || isDead || Time.time < invulnerableUntil)
            return;

        amount = DamageEvents.ModifyOutgoing(attacker, amount);
        foreach (var mod in GetComponents<IIncomingDamageModifier>())
            amount = mod.ModifyIncoming(amount, attacker);

        if (attacker != null && attacker != gameObject)
            lastAttacker = attacker;

        DamageEvents.RaiseDamaged(gameObject, attacker, amount);
        if (amount <= 0f) return; // fully absorbed (e.g. ice shield)

        LastDamageTime = Time.time;
        currentHealth = Mathf.Max(currentHealth - amount, 0);

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
            return;

        float before = currentHealth;
        currentHealth = Mathf.Min(currentHealth + amount, maxHealth);

        if (currentHealth > before)
        {
            UpdateUI();
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
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

    // ---------- Heart icons ----------
    void CacheStockLayout()
    {
        stockSlots.Clear();
        if (stock == null) return;

        var positions = new List<Vector2>();
        foreach (var s in stock)
        {
            if (s == null) continue;
            stockSlots.Add(s);
            if (s.transform is RectTransform rt) positions.Add(rt.anchoredPosition);
        }
        if (positions.Count == 0) return;

        Vector2 sum = Vector2.zero;
        foreach (var p in positions) sum += p;
        stockCenter = sum / positions.Count;
        if (positions.Count >= 2) stockSpacing = positions[1] - positions[0];
    }

    /// Shows one heart per remaining life (current + extra), centered under the crest.
    void UpdateStockUI()
    {
        if (stockSlots.Count == 0) return;

        int lives = isDead ? 0 : stockCount + 1;

        // Wizards with more hearts than the scene has icons get cloned icons
        while (stockSlots.Count < lives)
        {
            var src = stockSlots[stockSlots.Count - 1];
            var clone = Instantiate(src, src.transform.parent);
            clone.name = $"{src.name} (extra)";
            stockSlots.Add(clone);
        }

        for (int i = 0; i < stockSlots.Count; i++)
        {
            bool show = i < lives;
            stockSlots[i].SetActive(show);
            if (show && stockSlots[i].transform is RectTransform rt)
                rt.anchoredPosition = stockCenter + stockSpacing * (i - (lives - 1) * 0.5f);
        }
    }

    // deactivate the player
    private void Die()
    {
        isDead = true;
        UpdateStockUI();
        var movement = GetComponent<PlayerMovement3D>();
        if (movement?.gamepad != null)
            movement.gamepad.SetMotorSpeeds(0, 0);

        DamageEvents.RaiseKilled(gameObject, lastAttacker, transform.position);
        // Soulfracture marks explode on death too, not just on stock loss
        cursedPlayer?.OnStockLost();

        OnDeath?.Invoke();
        gameObject.SetActive(false);
    }

    private void StockDamage()
    {
        stockCount = Mathf.Max(stockCount - 1, 0);
        UpdateStockUI();

        DamageEvents.RaiseKilled(gameObject, lastAttacker, transform.position);

        // Trigger curse explosion if applicable
        cursedPlayer?.OnStockLost();

        // Reset health for next stock
        currentHealth = maxHealth;
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        invincible = true;
        Invoke(nameof(ResetInvincibility), 0.1f);
    }


    private void ResetInvincibility()
    {
        invincible = false;
    }

    private IEnumerator StopRumble(Gamepad pad, float delay)
    {
        yield return new WaitForSeconds(delay);
        pad.SetMotorSpeeds(0, 0);
    }
}
