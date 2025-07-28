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

    [Header("Jumping")]
    public float gravity   = -9.81f;
    public float jumpForce = 5f;
    public int   maxJumps  = 2;

    [Header("Dash")]
    public float dashSpeed     = 12f;
    public float dashDuration  = 0.2f;
    public float dashCooldown  = 1.0f;
    public Image dashBar;
    public Sprite[] dashBarSprites;

    [Header("Controller (set at runtime)")]
    public Gamepad gamepad = null;
    public int playerIndex = 0;
    public WizardData wizard;        // optional if you want wizard stats to tweak movement

    // Internal
    CharacterController controller;
    int    jumpsRemaining;
    float  verticalVelocity = 0f;
    Vector3 lastDirection   = Vector3.forward;
    static readonly Quaternion IsoRotation = Quaternion.Euler(0, 45f, 0);

    bool  isDashing = false;
    float dashTimer = 0f;
    float dashCooldownTimer = 0f;
    Coroutine dashRefill;

    public float currentMoveSpeed;

    #region Public API
    /// <summary>Call from MultiplayerManager AFTER spawning/activating this player.</summary>
    public void Setup(int index, Gamepad pad, WizardData wiz = null)
    {
        playerIndex = index;
        gamepad     = pad;
        wizard      = wiz;

        // If wizard modifies stats, do it here:
        currentMoveSpeed = baseMoveSpeed;
        // Example: currentMoveSpeed += (wiz ? wiz.speedBonus : 0f);

        // Reset state (e.g. when reusing prefab)
        jumpsRemaining     = maxJumps;
        verticalVelocity   = -0.5f;
        isDashing          = false;
        dashTimer          = 0f;
        dashCooldownTimer  = 0f;
        lastDirection      = Vector3.forward;

        // UI reset
        if (dashBar && dashBarSprites != null && dashBarSprites.Length > 0)
            dashBar.sprite = dashBarSprites[^1]; // full bar
    }
    #endregion

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        currentMoveSpeed = baseMoveSpeed;
        jumpsRemaining = maxJumps;
    }

    void Update()
    {
        if (gamepad == null) return;

        // Cooldowns
        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - Time.deltaTime);
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f) isDashing = false;
        }

        // Input
        Vector2 stick = gamepad.leftStick.ReadValue();
        if (stick.sqrMagnitude < 0.01f) stick = Vector2.zero;

        // Jump
        if (!isDashing && gamepad.buttonSouth.wasPressedThisFrame &&
            (controller.isGrounded || jumpsRemaining > 0))
        {
            verticalVelocity = jumpForce;
            if (!controller.isGrounded) jumpsRemaining--;
        }

        // Dash
        if (!isDashing && dashCooldownTimer <= 0f &&
            stick.sqrMagnitude > 0.01f &&
            gamepad.buttonEast.wasPressedThisFrame)
        {
            StartDash();
        }

        // Direction
        Vector3 planar  = new Vector3(stick.x, 0, stick.y).normalized;
        Vector3 isoDir  = IsoRotation * planar;
        if (isoDir.sqrMagnitude > 0.01f && !isDashing)
            lastDirection = isoDir;

        Vector3 horiz = isDashing
            ? lastDirection * dashSpeed
            : lastDirection * currentMoveSpeed * stick.magnitude;

        // Gravity & jump reset
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -0.5f;
            jumpsRemaining = maxJumps - 1; // you already used one when you jumped
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // Move
        Vector3 motion = new Vector3(horiz.x, verticalVelocity, horiz.z);
        controller.Move(motion * Time.deltaTime);

        // Rotate
        if (stick.sqrMagnitude > 0.01f && !isDashing)
        {
            Quaternion targetRot = Quaternion.LookRotation(lastDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        // Anim
        if (animator) animator.SetFloat("Speed", isDashing ? 1f : stick.magnitude);
    }

    void StartDash()
    {
        isDashing         = true;
        dashTimer         = dashDuration;
        dashCooldownTimer = dashCooldown;
        StartDashRefill();
        // TODO: dash FX / SFX
    }

    void StartDashRefill()
    {
        if (!dashBar || dashBarSprites == null || dashBarSprites.Length == 0)
            return;

        dashBar.sprite = dashBarSprites[0];     // empty immediately

        if (dashRefill != null) StopCoroutine(dashRefill);
        dashRefill = StartCoroutine(DiscreteRefillCoroutine());
    }

    IEnumerator DiscreteRefillCoroutine()
    {
        int steps = dashBarSprites.Length - 1;
        if (steps <= 0) yield break;

        float stepTime = dashCooldown / steps;

        for (int i = 1; i <= steps; i++)
        {
            yield return new WaitForSeconds(stepTime);
            if (dashBar) dashBar.sprite = dashBarSprites[i];
        }

        dashRefill = null;
    }
}
