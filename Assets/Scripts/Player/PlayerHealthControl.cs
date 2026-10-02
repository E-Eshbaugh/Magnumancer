using UnityEngine;
using System;
using System.Collections.Generic;

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
    public CrestUIController healthMask;
    [Tooltip("Legacy stock icons: only one is shown for the single health pool")]
    public GameObject[] stock;
    // Kept for scene serialization and older callers; extra stocks are no longer used.
    public int stockCount = 0;
    public PlayerMovement3D movement;

    [Header("Health and Revive")]
    [Min(1f)] public float healthPerHeart = 100f;
    [Min(0.1f)] public float reviveRadius = 2.5f;
    [Min(0.1f)] public float reviveDuration = 4f;
    [Range(0.01f, 1f)] public float reviveHealthFraction = 0.5f;
    [UnityEngine.Serialization.FormerlySerializedAs("respawnInvulnerability")]
    public float reviveInvulnerability = 2f;
    [Tooltip("Brief protection after a zombie hit prevents an overlapping crowd from downing a wizard in one frame")]
    [Min(0f)] public float hordeHitGrace = 0.35f;
    public bool IsDowned { get; private set; }
    public bool IsStanding => !isDead && !IsDowned && isActiveAndEnabled;
    public float ReviveProgress { get; private set; }
    public event Action OnDowned;
    public event Action OnRevived;

    static readonly List<PlayerHealthControl> activePlayers = new();
    public static IReadOnlyList<PlayerHealthControl> ActivePlayers => activePlayers;
    WizardReviveCircle reviveCircle;

    public static bool IsIncapacitated(Component component)
    {
        var health = component.GetComponentInParent<PlayerHealthControl>();
        return health != null && !health.IsStanding;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => activePlayers.Clear();
    void OnEnable() { if (!activePlayers.Contains(this)) activePlayers.Add(this); }
    void OnDisable() => activePlayers.Remove(this);

    /// Time.time of the last hit that actually did damage (Verdant Resurgence)
    public float LastDamageTime { get; private set; } = -999f;
    public bool IsDead => isDead;

    float invulnerableUntil;
    GameObject lastAttacker;

    void Awake()
    {
        movement = GetComponent<PlayerMovement3D>();
        currentHealth = maxHealth;
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (cursedPlayer == null)
            cursedPlayer = GetComponent<CursedPlayer>();

        stockCount = 0;
        UpdateStockUI();
    }

    /// Each heart in wizard select contributes to one continuous health pool.
    public void SetHealthStat(int hearts)
    {
        maxHealth = Mathf.Max(1, hearts) * Mathf.Max(1f, healthPerHeart);
        currentHealth = maxHealth;
        stockCount = 0;
        UpdateStockUI();
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void SetLives(int hearts) => SetHealthStat(hearts);
    public int LivesLeft => IsStanding ? 1 : 0;

    /// Heart Relic grows the same health pool instead of granting a respawn.
    public void AddLife()
    {
        if (!IsStanding) return;
        float bonus = Mathf.Max(1f, healthPerHeart);
        maxHealth += bonus;
        currentHealth += bonus;
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// Ignore all damage for a while (Last Rites).
    public void GrantInvulnerability(float seconds)
        => invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);

    /// <summary>
    /// Called by bullets, explosions, hazards and abilities. attacker may be null.
    /// </summary>
    public void TakeDamage(float amount, GameObject attacker = null)
    {
        if (!Teams.CanHarm(gameObject, attacker)) return;
        // isDead: lingering hazards (lava, poison) can still tick on a dead player
        if (amount <= 0 || invincible || !IsStanding || Time.time < invulnerableUntil)
            return;

        amount = DamageEvents.ModifyOutgoing(attacker, amount);
        foreach (var mod in GetComponents<IIncomingDamageModifier>())
            amount = mod.ModifyIncoming(amount, attacker);

        if (attacker != null && attacker != gameObject)
            lastAttacker = attacker;

        DamageEvents.RaiseDamaged(gameObject, attacker, amount);
        if (amount <= 0f) return; // fully absorbed (e.g. ice shield)

        LastDamageTime = Time.time;
        if (Teams.HumansVsHorde) GrantInvulnerability(hordeHitGrace);
        currentHealth = Mathf.Max(currentHealth - amount, 0);

        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0) Down();
    }

    /// Sets health to a fraction of max (Soul Swap). Never kills: at least 1 HP.
    public void SetHealthFraction(float fraction)
    {
        if (!IsStanding) return;
        currentHealth = Mathf.Clamp(Mathf.Round(maxHealth * Mathf.Clamp01(fraction)), 1f, maxHealth);
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || !IsStanding || currentHealth <= 0)
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

    void UpdateStockUI()
    {
        if (stock == null) return;
        for (int i = 0; i < stock.Length; i++)
            if (stock[i] != null) stock[i].SetActive(i == 0);
    }

    void Down()
    {
        IsDowned = true; // Set before callbacks so lingering damage cannot down twice.
        ReviveProgress = 0f;
        if (movement != null) movement.StopForDowned();
        foreach (var ammo in GetComponentsInChildren<AmmoControl>())
            ammo.SetFiringBlocked("downed", true);
        reviveCircle = GetComponent<WizardReviveCircle>();
        if (reviveCircle == null) reviveCircle = gameObject.AddComponent<WizardReviveCircle>();
        reviveCircle.Show(this);
        DamageEvents.RaiseKilled(gameObject, lastAttacker, transform.position);
        cursedPlayer?.OnStockLost();
        OnDowned?.Invoke();
        ResolveTeamWipe();
    }

    void Update()
    {
        if (!IsDowned || isDead || GamePause.InputBlocked) return;
        if (ResolveTeamWipe()) return;

        bool helping = false;
        foreach (var teammate in activePlayers)
        {
            if (CanBeRevivedBy(teammate)) { helping = true; break; }
        }
        // Continuous presence is required; additional allies do not multiply the speed.
        ReviveProgress = helping
            ? Mathf.Clamp01(ReviveProgress + Time.deltaTime / Mathf.Max(0.1f, reviveDuration)) : 0f;
        if (ReviveProgress >= 1f) Revive();
    }

    public bool CanBeRevivedBy(PlayerHealthControl teammate)
    {
        if (!IsDowned || isDead || teammate == null || !teammate.IsStanding
            || !Teams.SameTeam(gameObject, teammate.gameObject)) return false;
        Vector3 offset = teammate.transform.position - transform.position;
        if (Mathf.Abs(offset.y) > 1f || (teammate.movement != null && teammate.movement.IsAirborne)) return false;
        offset.y = 0f;
        return offset.sqrMagnitude <= reviveRadius * reviveRadius;
    }

    bool ResolveTeamWipe()
    {
        foreach (var teammate in activePlayers)
            if (teammate.IsStanding && Teams.SameTeam(gameObject, teammate.gameObject)) return false;

        // Nobody can perform a revive. Includes a solo run and a free-for-all elimination.
        var fallen = activePlayers.ToArray();
        foreach (var player in fallen)
            if (player.IsDowned && (player == this || Teams.SameTeam(gameObject, player.gameObject)))
                player.Die();
        return true;
    }

    void Revive()
    {
        IsDowned = false;
        ReviveProgress = 0f;
        currentHealth = Mathf.Max(1f, maxHealth * Mathf.Clamp(reviveHealthFraction, 0.01f, 1f));
        lastAttacker = null;
        LastDamageTime = Time.time;
        if (reviveCircle != null) reviveCircle.Hide();
        foreach (var ammo in GetComponentsInChildren<AmmoControl>())
            ammo.SetFiringBlocked("downed", false);
        GrantInvulnerability(reviveInvulnerability);
        UpdateUI();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnRevived?.Invoke();
        WizardSpawnEffect.Play(gameObject, movement != null ? movement.wizard : null);
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        IsDowned = false;
        ReviveProgress = 0f;
        if (reviveCircle != null) reviveCircle.Hide();
        WizardDeathEffect.Play(transform.position, movement != null ? movement.wizard : null, final: true, gameObject);
        if (healthMask != null) UIGreyOut.Apply(healthMask.gameObject);
        OnDeath?.Invoke();
        gameObject.SetActive(false);
    }
}
