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
    int maxLives;   // lives at match start: greyed-out hearts shown once eliminated
    public PlayerMovement3D movement;

    [Header("Respawn")]
    [Tooltip("After losing a life you respawn at your starting spot and can't be hurt for this long")]
    public float respawnInvulnerability = 2f;

    /// Time.time of the last hit that actually did damage (Verdant Resurgence)
    public float LastDamageTime { get; private set; } = -999f;
    public bool IsDead => isDead;

    float invulnerableUntil;
    GameObject lastAttacker;

    Vector3 spawnPosition;
    Quaternion spawnRotation = Quaternion.identity;
    bool hasSpawnPoint;

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

        maxLives = stockCount + 1;
        CacheStockLayout();
        UpdateStockUI();
    }

    /// <summary>
    /// Sets total lives from the wizard's heart count (1 heart = no extra lives).
    /// </summary>
    public void SetLives(int hearts)
    {
        stockCount = Mathf.Max(0, hearts - 1);
        maxLives = stockCount + 1;
        UpdateStockUI();
    }

    /// Where this player starts the match — and comes back to after losing a life.
    public void SetSpawnPoint(Vector3 position, Quaternion rotation)
    {
        spawnPosition = position;
        spawnRotation = rotation;
        hasSpawnPoint = true;
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

    /// Sets health to a fraction of max (Soul Swap). Never kills: at least 1 HP.
    public void SetHealthFraction(float fraction)
    {
        if (isDead) return;
        currentHealth = Mathf.Clamp(Mathf.Round(maxHealth * Mathf.Clamp01(fraction)), 1f, maxHealth);
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
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
    /// Once eliminated, every heart they started with comes back, greyed out with the panel.
    void UpdateStockUI()
    {
        if (stockSlots.Count == 0) return;

        int lives = isDead ? maxLives : stockCount + 1;

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

    // Out of lives: blow up, grey out the HUD, deactivate the player
    private void Die()
    {
        isDead = true;
        UpdateStockUI();
        DamageEvents.RaiseKilled(gameObject, lastAttacker, transform.position);
        // Soulfracture marks explode on death too, not just on stock loss
        cursedPlayer?.OnStockLost();

        WizardDeathEffect.Play(transform.position, Wizard(), final: true);
        if (healthMask != null) UIGreyOut.Apply(healthMask.gameObject);

        OnDeath?.Invoke();
        gameObject.SetActive(false);
    }

    WizardData Wizard()
    {
        if (movement == null) movement = GetComponent<PlayerMovement3D>();
        return movement != null ? movement.wizard : null;
    }

    private void StockDamage()
    {
        stockCount = Mathf.Max(stockCount - 1, 0);
        UpdateStockUI();

        DamageEvents.RaiseKilled(gameObject, lastAttacker, transform.position);

        // Trigger curse explosion if applicable
        cursedPlayer?.OnStockLost();

        // A smaller version of the death fireball where they fell (they respawn elsewhere)
        WizardDeathEffect.Play(transform.position, Wizard(), final: false);

        // Reset health for next stock
        currentHealth = maxHealth;
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        invincible = true;
        Invoke(nameof(ResetInvincibility), 0.1f);

        Respawn();
    }

    /// Back to the starting spot with full ammo, arriving in a bolt of the wizard's color.
    void Respawn()
    {
        if (movement == null) movement = GetComponent<PlayerMovement3D>();

        if (hasSpawnPoint && movement != null)
            movement.Teleport(spawnPosition, spawnRotation);

        foreach (var ammo in GetComponentsInChildren<AmmoControl>())
            ammo.RefillAll();

        GrantInvulnerability(respawnInvulnerability);
        WizardSpawnEffect.Play(gameObject, movement != null ? movement.wizard : null);
    }


    private void ResetInvincibility()
    {
        invincible = false;
    }

}
