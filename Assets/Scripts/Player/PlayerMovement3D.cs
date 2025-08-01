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
    public Image dashBar;
    public Sprite[] dashBarSprites;

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
        knockbackVelocity = force;
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
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        currentMoveSpeed = baseMoveSpeed;
        jumpsRemaining = maxJumps;
    }

    void Update()
    {
        if (gamepad == null) return;

        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - Time.deltaTime);

        Vector2 stick = gamepad.leftStick.ReadValue();
        if (stick.sqrMagnitude < 0.01f) stick = Vector2.zero;

        if (!isDashing && gamepad.buttonSouth.wasPressedThisFrame &&
            (controller.isGrounded || jumpsRemaining > 0))
        {
            verticalVelocity = jumpForce;
            if (!controller.isGrounded) jumpsRemaining--;
        }

        if (!isDashing && dashCooldownTimer <= 0f &&
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
            verticalVelocity = -0.5f;
            jumpsRemaining = maxJumps - 1;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 horiz = isDashing
            ? lastDirection * dashSpeed
            : lastDirection * currentMoveSpeed * stick.magnitude;

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

        Vector3 finalMotion = moveDirection + knockbackVelocity;
        controller.Move(finalMotion * Time.fixedDeltaTime);

        if (knockbackVelocity.sqrMagnitude > 0.01f)
        {
            knockbackVelocity = Vector3.Lerp(knockbackVelocity, Vector3.zero, knockbackDecayRate * Time.fixedDeltaTime);
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

    void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        StartDashRefill();
    }

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

        float stepTime = dashCooldown / steps;

        for (int i = 1; i <= steps; i++)
        {
            yield return new WaitForSeconds(stepTime);
            if (dashBar) dashBar.sprite = dashBarSprites[i];
        }

        dashRefill = null;
    }

    public void SetMoveSpeedMultiplier(float value)
    {
        moveSpeedMultiplier = value;
    }
}
