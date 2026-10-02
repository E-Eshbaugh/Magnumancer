// === PlayerMovement3D.cs ===
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement3D : MonoBehaviour
{
    [Header("Movement")]
    public Animator animator;
    public float baseMoveSpeed = 5f;
    public float moveSpeedMultiplier = 1f;

    [Header("Jumping")]
    public float gravity = -9.81f;
    public float jumpForce = 5f;
    public int maxJumps = 2;

    [Header("Dash")]
    public float dashSpeed = 12f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1.0f;
    [Tooltip("Scales dashCooldown (e.g. Tidebound's Undercurrent)")]
    public float dashCooldownMultiplier = 1f;
    public Image dashBar;
    public Sprite[] dashBarSprites;

    [Header("Weapon Weight")]
    [Tooltip("Move speed while holding a gun of weight 0..5 (index = weight)")]
    public float[] heldWeightSpeed = { 1f, 0.97f, 0.92f, 0.86f, 0.78f, 0.68f };
    [Tooltip("Total loadout weight you can carry before it starts slowing you")]
    public int packWeightAllowance = 8;
    [Tooltip("Move speed lost per point of loadout weight over the allowance")]
    public float packSlowPerPoint = 0.015f;
    public float maxPackSlow = 0.15f;
    [Tooltip("Knockback resisted per point of held weight (heavy gunners are hard to shove)")]
    public float knockbackResistPerPoint = 0.08f;
    [Tooltip("Dash cooldown added per point of held weight")]
    public float dashCooldownPerPoint = 0.06f;
    [Tooltip("Scales every weight speed penalty (Granite Vow's Stonebind halves it)")]
    public float weightPenaltyScale = 1f;

    [Header("Knockback")]
    [Tooltip("Scales incoming knockback (e.g. Granite Vow's Stonebind)")]
    public float knockbackMultiplier = 1f;
    [Tooltip("Cap on the shove that stacks up from gunfire (units/sec)")]
    public float maxHitKnockback = 12f;

    /// Fired when a dash starts (used by Undercurrent / Lightning Reflex)
    public event System.Action OnDash;

    /// How hard the move stick is pushed (0..1)
    public float StickMagnitude { get; private set; }

    // Named speed multipliers (stun, freeze, weight, buffs) — moveSpeedMultiplier is their product
    readonly System.Collections.Generic.Dictionary<string, float> speedModifiers = new();

    [Header("Controller")]
    public Gamepad gamepad = null;
    public int playerIndex = 0;
    public WizardData wizard;

    CharacterController controller;
    int jumpsRemaining;
    float verticalVelocity = 0f;
    Vector3 moveDirection = Vector3.zero;
    Vector3 lastDirection = Vector3.forward;
    static readonly Quaternion IsoRotation = Quaternion.Euler(0, 45f, 0);

    bool isDashing = false;
    float dashTimer = 0f;
    float dashCooldownTimer = 0f;
    Coroutine dashRefill;

    public float currentMoveSpeed;

    // Knockback
    Vector3 knockbackVelocity = Vector3.zero;
    float knockbackDecayRate = 10f;

    public void ApplyKnockback(Vector3 force)
    {
        if (PlayerHealthControl.IsIncapacitated(this)) return;
        knockbackVelocity = force * knockbackMultiplier * WeightKnockbackScale;
    }

    /// Gunfire shoves: hits stack up (a shotgun volley pushes harder than one pellet), capped.
    public void AddKnockback(Vector3 force)
    {
        if (PlayerHealthControl.IsIncapacitated(this)) return;
        force.y = 0f;
        Vector3 v = knockbackVelocity + force * knockbackMultiplier * WeightKnockbackScale;
        knockbackVelocity = Vector3.ClampMagnitude(v, Mathf.Max(maxHitKnockback, knockbackVelocity.magnitude));
    }

    public void Setup(int index, Gamepad pad, WizardData wiz = null)
    {
        playerIndex = index;
        gamepad = pad;
        wizard = wiz;
        currentMoveSpeed = baseMoveSpeed;
        jumpsRemaining = maxJumps;
        verticalVelocity = -0.5f;
        isDashing = false;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
        lastDirection = Vector3.forward;

        if (dashBar && dashBarSprites != null && dashBarSprites.Length > 0)
            dashBar.sprite = dashBarSprites[^1];

        SetSpeedModifier("wizard", wiz != null ? wiz.moveSpeedMultiplier : 1f);
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        currentMoveSpeed = baseMoveSpeed;
        jumpsRemaining = maxJumps;
    }

    void Update()
    {
        if (gamepad == null || PlayerHealthControl.IsIncapacitated(this)) return;

        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - Time.deltaTime);

        // Pause menu owns the controller (A/B/sticks) while it's open
        bool inputOk = !GamePause.InputBlocked;

        Vector2 stick = inputOk ? gamepad.leftStick.ReadValue() : Vector2.zero;
        if (stick.sqrMagnitude < 0.01f) stick = Vector2.zero;
        StickMagnitude = stick.magnitude;

        airTime = controller.isGrounded ? 0f : airTime + Time.deltaTime;

        if (inputOk && !isDashing && CanJump && gamepad.buttonSouth.wasPressedThisFrame &&
            (controller.isGrounded || jumpsRemaining > 0))
        {
            verticalVelocity = jumpForce;
            if (!controller.isGrounded) jumpsRemaining--;
        }

        if (inputOk && !isDashing && CanDash && dashCooldownTimer <= 0f &&
            stick.sqrMagnitude > 0.01f &&
            gamepad.buttonEast.wasPressedThisFrame)
        {
            StartDash();
        }

        Vector3 planar = new Vector3(stick.x, 0, stick.y).normalized;
        Vector3 isoDir = IsoRotation * planar;

        if (isoDir.sqrMagnitude > 0.01f && !isDashing)
            lastDirection = isoDir;

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            // thump on a real fall (a normal jump lands around -5)
            if (verticalVelocity < -7f)
                Rumble.Land(gamepad, Mathf.Clamp01(-verticalVelocity / 20f));
            verticalVelocity = -0.5f;
            jumpsRemaining = maxJumps - 1;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 wanted = isDashing
            ? lastDirection * dashSpeed
            : lastDirection * currentMoveSpeed * moveSpeedMultiplier * stick.magnitude;

        // Full traction snaps to the stick; on ice you build up and bleed off speed slowly
        if (isDashing || traction >= 0.999f)
            slideVelocity = wanted;
        else
            slideVelocity = Vector3.MoveTowards(slideVelocity, wanted, Mathf.Lerp(4f, 60f, traction) * Time.deltaTime);
        Vector3 horiz = slideVelocity;

        moveDirection = new Vector3(horiz.x, verticalVelocity, horiz.z);

        if (stick.sqrMagnitude > 0.01f && !isDashing)
        {
            Quaternion targetRot = Quaternion.LookRotation(lastDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        if (animator) animator.SetFloat("Speed", isDashing ? 1f : stick.magnitude);
    }

    void FixedUpdate()
    {
        if (controller == null || gamepad == null) return;
        if (PlayerHealthControl.IsIncapacitated(this))
        {
            // Fall to the floor if downed mid-jump, without steering or knockback.
            verticalVelocity = controller.isGrounded ? -0.5f : verticalVelocity + gravity * Time.fixedDeltaTime;
            controller.Move(Vector3.up * verticalVelocity * Time.fixedDeltaTime);
            return;
        }

        Vector3 finalMotion = moveDirection + knockbackVelocity;
        controller.Move(finalMotion * Time.fixedDeltaTime);

        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero,
                knockbackDecayRate * Mathf.Lerp(0.3f, 1f, traction) * Time.fixedDeltaTime); // shoves slide further on ice
        }
        else
        {
            knockbackVelocity = Vector3.zero;
        }

        if (isDashing)
        {
            dashTimer -= Time.fixedDeltaTime;
            if (dashTimer <= 0f) isDashing = false;
        }
    }

    public void StopForDowned()
    {
        CancelDash();
        knockbackVelocity = slideVelocity = moveDirection = Vector3.zero;
        verticalVelocity = -0.5f;
        StickMagnitude = 0f;
        if (animator != null) animator.SetFloat("Speed", 0f);
    }

    /// Moves the player instantly (respawn), clearing momentum, knockback and dashes.
    public void Teleport(Vector3 position, Quaternion rotation)
    {
        bool wasEnabled = controller.enabled;
        controller.enabled = false; // CharacterController overrides transform moves while enabled
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = wasEnabled;

        verticalVelocity = -0.5f;
        knockbackVelocity = Vector3.zero;
        moveDirection = Vector3.zero;
        slideVelocity = Vector3.zero;
        isDashing = false;
        dashTimer = 0f;
        jumpsRemaining = maxJumps;
        lastDirection = rotation * Vector3.forward;
        lastDirection.y = 0f;
        if (lastDirection.sqrMagnitude < 0.01f) lastDirection = Vector3.forward;
        lastDirection.Normalize();
    }

    void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = EffectiveDashCooldown;
        StartDashRefill();
        Rumble.Dash(gamepad);
        OnDash?.Invoke();
    }

    float EffectiveDashCooldown => dashCooldown * dashCooldownMultiplier * dashCooldownBonus
                                   * (1f + dashCooldownPerPoint * heldWeight);

    // Named dash-cooldown multipliers from synergies (Undercurrent keeps using dashCooldownMultiplier)
    readonly System.Collections.Generic.Dictionary<string, float> dashMods = new();
    float dashCooldownBonus = 1f;

    public void SetDashCooldownModifier(string key, float mult)
    {
        if (Mathf.Approximately(mult, 1f)) dashMods.Remove(key); else dashMods[key] = mult;
        dashCooldownBonus = 1f;
        foreach (var v in dashMods.Values) dashCooldownBonus *= v;
    }

    public bool IsDashing => isDashing;

    // ---------- Jumping state ----------
    readonly System.Collections.Generic.HashSet<string> jumpBlocks = new();
    float airTime;

    /// Stunned, rooted or frozen players can't jump (named so overlapping effects don't clear each other)
    public void SetJumpBlocked(string key, bool blocked)
    {
        if (blocked) jumpBlocks.Add(key); else jumpBlocks.Remove(key);
    }

    public bool CanJump => jumpBlocks.Count == 0;

    // ---------- Dash blocks (Mudslide) ----------
    readonly System.Collections.Generic.HashSet<string> dashBlocks = new();

    /// Mired players can't dash (named so overlapping effects don't clear each other)
    public void SetDashBlocked(string key, bool blocked)
    {
        if (blocked) dashBlocks.Add(key); else dashBlocks.Remove(key);
    }

    public bool CanDash => dashBlocks.Count == 0;

    // ---------- Traction (Brittle ice) ----------
    // 1 = normal (movement snaps to the stick). Lower = slippery: you speed up and slow
    // down gradually, keep sliding after a dash, and knockback carries further.
    readonly System.Collections.Generic.Dictionary<string, float> tractionMods = new();
    float traction = 1f;
    Vector3 slideVelocity;

    public float Traction => traction;

    /// Named traction (the slipperiest one wins)
    public void SetTraction(string key, float value)
    {
        tractionMods[key] = Mathf.Clamp01(value);
        RecalculateTraction();
    }

    public void ClearTraction(string key)
    {
        if (tractionMods.Remove(key)) RecalculateTraction();
    }

    void RecalculateTraction()
    {
        traction = 1f;
        foreach (var v in tractionMods.Values) traction = Mathf.Min(traction, v);
    }

    /// Off the ground for more than a moment (a real jump or fall, not a step down).
    /// Ground shockwaves like Seismic Judgement pass under airborne players.
    public bool IsAirborne => airTime > 0.08f;

    /// Stops the current dash's movement (Stormrunner replaces it with a blink)
    public void CancelDash()
    {
        isDashing = false;
        dashTimer = 0f;
    }

    /// How far above the arena floor the player is standing (e.g. on an Earthwork
    /// Parapet). Guns angle their shots down by this much so high ground can still hit
    /// people below. Set by whatever lifted them; 0 normally.
    [HideInInspector] public float elevation;

    /// Where the left stick points in world space (or where you're facing)
    public Vector3 MoveDirection => lastDirection;

    void StartDashRefill()
    {
        if (!dashBar || dashBarSprites == null || dashBarSprites.Length == 0)
            return;

        dashBar.sprite = dashBarSprites[0];

        if (dashRefill != null) StopCoroutine(dashRefill);
        dashRefill = StartCoroutine(DiscreteRefillCoroutine());
    }

    IEnumerator DiscreteRefillCoroutine()
    {
        int steps = dashBarSprites.Length - 1;
        if (steps <= 0) yield break;

        float stepTime = EffectiveDashCooldown / steps;

        for (int i = 1; i <= steps; i++)
        {
            yield return new WaitForSeconds(stepTime);
            if (dashBar) dashBar.sprite = dashBarSprites[i];
        }

        dashRefill = null;
    }

    /// Stun slow (kept for StunEffect)
    public void SetMoveSpeedMultiplier(float value) => SetSpeedModifier("stun", value);

    /// Sets a named speed multiplier; all active modifiers multiply together.
    public void SetSpeedModifier(string key, float multiplier)
    {
        if (Mathf.Approximately(multiplier, 1f)) speedModifiers.Remove(key);
        else speedModifiers[key] = multiplier;
        RecalculateSpeedMultiplier();
    }

    public void ClearSpeedModifier(string key)
    {
        if (speedModifiers.Remove(key)) RecalculateSpeedMultiplier();
    }

    // ---------- Weapon weight ----------
    // The gun in your hands sets most of the slowdown; everything else you carry adds a
    // little once the loadout gets heavy. Heavy guns also make you harder to shove and
    // slower to dash again, so light builds are zippy and heavy builds are tanks.

    int heldWeight, packWeight;

    public int HeldWeight => heldWeight;
    float WeightKnockbackScale => Mathf.Clamp01(1f - knockbackResistPerPoint * heldWeight);

    /// held = equipped gun's weight, pack = total weight of the whole loadout
    public void SetCarriedWeight(int held, int pack)
    {
        heldWeight = Mathf.Max(0, held);
        packWeight = Mathf.Max(0, pack);
        SetSpeedModifier("weight", WeightSpeed(heldWeight, packWeight, weightPenaltyScale, this));
    }

    public void SetCarriedWeight(int held) => SetCarriedWeight(held, packWeight);

    /// Speed multiplier for a held/pack weight (also used by the loadout screen)
    public static float WeightSpeed(int held, int pack, float penaltyScale = 1f, PlayerMovement3D tuning = null)
    {
        float[] table = tuning != null ? tuning.heldWeightSpeed : DefaultHeldSpeed;
        int allowance = tuning != null ? tuning.packWeightAllowance : 8;
        float perPoint = tuning != null ? tuning.packSlowPerPoint : 0.015f;
        float maxPack = tuning != null ? tuning.maxPackSlow : 0.15f;

        float heldMult = table.Length > 0 ? table[Mathf.Clamp(held, 0, table.Length - 1)] : 1f;
        float packMult = 1f - Mathf.Min(maxPack, perPoint * Mathf.Max(0, pack - allowance));
        float slow = 1f - heldMult * packMult;
        return Mathf.Max(0.4f, 1f - slow * penaltyScale);
    }

    static readonly float[] DefaultHeldSpeed = { 1f, 0.97f, 0.92f, 0.86f, 0.78f, 0.68f };

    void RecalculateSpeedMultiplier()
    {
        float m = 1f;
        foreach (var v in speedModifiers.Values) m *= v;
        moveSpeedMultiplier = m;
    }
}
