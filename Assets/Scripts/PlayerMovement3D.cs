using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement3D : MonoBehaviour
{
    [Header("Movement")]
    public Animator animator;
    public float moveSpeed = 5f;

    [Header("Jumping")]
    public float gravity = -9.81f;
    public float jumpForce = 5f;
    public int maxJumps = 2;

    [Header("Dash")]
    [Tooltip("Speed during dash")]
    public float dashSpeed = 12f;
    [Tooltip("How long the dash lasts (seconds)")]
    public float dashDuration = 0.2f;
    [Tooltip("Cooldown between dashes (seconds)")]
    public float dashCooldown = 1.0f;
    public Image dashBar;
    public Sprite[] dashBarSprites;

    [Header("Controller Settings")]
    [Tooltip("Set at runtime by MultiplayerManager")]
    public Gamepad gamepad = null;

    // internal state
    private CharacterController controller;
    private int jumpsRemaining;
    private float verticalVelocity = 0f;
    private Vector3 lastDirection = Vector3.forward;
    private static readonly Quaternion IsoRotation = Quaternion.Euler(0, 45f, 0);

    // dash state
    private bool isDashing = false;
    private float dashTimer = 0f;
    private float dashCooldownTimer = 0f;
    private Coroutine dashRefill;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        jumpsRemaining = maxJumps;
    }

    void Update()
    {
        if (gamepad == null)
            return;

        // Timers
        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - Time.deltaTime);
        if (isDashing)
        {
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
                isDashing = false;
        }

        // Read stick input
        Vector2 stick = gamepad.leftStick.ReadValue();
        if (stick.magnitude < 0.1f) stick = Vector2.zero;

        // Jump
        if (!isDashing && gamepad.buttonSouth.wasPressedThisFrame &&
            (controller.isGrounded || jumpsRemaining > 0))
        {
            verticalVelocity = jumpForce;
            if (!controller.isGrounded)
                jumpsRemaining--;
        }

        // Dash input
        if (!isDashing && dashCooldownTimer <= 0f && stick.sqrMagnitude > 0.01f &&
            gamepad.buttonEast.wasPressedThisFrame)
        {
            StartDash();
        }

        // Calculate directions
        Vector3 planar = new Vector3(stick.x, 0, stick.y).normalized;
        Vector3 isoDir = IsoRotation * planar;
        if (isoDir.sqrMagnitude > 0.01f && !isDashing)
            lastDirection = isoDir;

        Vector3 horiz = isDashing
            ? lastDirection * dashSpeed
            : lastDirection * moveSpeed * stick.magnitude;

        // Gravity & reset jumps
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -0.5f;
            jumpsRemaining = maxJumps - 1;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // Move
        Vector3 motion = new Vector3(horiz.x, verticalVelocity, horiz.z);
        controller.Move(motion * Time.deltaTime);

        // Rotate towards movement
        if (stick.sqrMagnitude > 0.01f && !isDashing)
        {
            var targetRot = Quaternion.LookRotation(lastDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
        }

        // Animate
        if (animator != null)
            animator.SetFloat("Speed", isDashing ? 1f : stick.magnitude);
    }

    private void StartDash()
    {
        isDashing = true;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        StartDashRefill();
        // TODO: play dash VFX/sound here
    }

    public void StartDashRefill()
    {
        // Immediately show empty sprite
        dashBar.sprite = dashBarSprites[0];

        // Kick off (or restart) the refill coroutine
        if (dashRefill != null)
            StopCoroutine(dashRefill);
        dashRefill = StartCoroutine(DiscreteRefillCoroutine());
    }

    private IEnumerator DiscreteRefillCoroutine()
    {
        int steps = dashBarSprites.Length - 1;      // e.g. 5 steps for 6 sprites
        float stepTime = dashCooldown / steps;    // how long between each sprite change

        for (int i = 1; i <= steps; i++)
        {
            // wait stepTime seconds
            yield return new WaitForSeconds(stepTime);

            // swap in the next sprite (20%, 40%, …)
            dashBar.sprite = dashBarSprites[i];
        }

        dashRefill = null;
    }

    
}

