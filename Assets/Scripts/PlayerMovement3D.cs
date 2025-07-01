using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement3D : MonoBehaviour
{
    [Header("Movement")]
    public Animator animator;           // Optional: for jump/land animations
    public float moveSpeed = 5f;

    [Header("Jumping")]
    public float gravity = -9.81f;
    public float jumpForce = 5f;
    public int maxJumps = 2;
    private int jumpsRemaining;

    [Header("Slam")]
    public float slamPause = 0.2f;
    public float slamForce = 25f;
    private bool slamming;

    private float verticalVelocity;
    private Vector2 moveInput;
    private Vector3 lastDirection = Vector3.forward;
    private CharacterController controller;
    private static readonly Quaternion IsoRotation = Quaternion.Euler(0, 45f, 0);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        jumpsRemaining = maxJumps;
    }

    public void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    public void OnJump(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || slamming) return;

        // Jump if grounded or have jumps left
        if (controller.isGrounded || jumpsRemaining > 0)
        {
            verticalVelocity = jumpForce;
            if (!controller.isGrounded)
                jumpsRemaining--;
        }
    }

    public void OnSlam(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed || slamming) return;

        // Only slam if you're airborne
        if (!controller.isGrounded)
            StartCoroutine(DoSlam());
    }

    private IEnumerator DoSlam()
    {
        slamming = true;
        // Pause in the air
        float oldVel = verticalVelocity;
        verticalVelocity = 0f;
        yield return new WaitForSeconds(slamPause);

        // Slam downward
        verticalVelocity = -slamForce;
        slamming = false;
    }

    void Update()
    {
        // — Horizontal Movement & Facing —
        Vector2 inp = moveInput.magnitude < .1f ? Vector2.zero : moveInput;
        Vector3 planar = new Vector3(inp.x, 0, inp.y).normalized;
        Vector3 isoDir = IsoRotation * planar;
        if (isoDir.sqrMagnitude > .01f && !slamming)
            lastDirection = isoDir;

        Vector3 horiz = lastDirection * moveSpeed * inp.magnitude;
        if (slamming) horiz = Vector3.zero;  // lock horizontal during slam

        // — Gravity & Jump Reset —
        if (controller.isGrounded)
        {
            // Stick to ground
            if (verticalVelocity < 0f) verticalVelocity = -0.5f;
            // Reset jumps
            jumpsRemaining = maxJumps - 1;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // — Move Character —
        Vector3 motion = new Vector3(horiz.x, verticalVelocity, horiz.z);
        controller.Move(motion * Time.deltaTime);

        // — Rotate to Face Movement —
        if (inp.sqrMagnitude > .01f && !slamming)
        {
            Quaternion target = Quaternion.LookRotation(lastDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 10f * Time.deltaTime);
        }

        // — Animate Speed —
        if (animator != null)
            animator.SetFloat("Speed", inp.magnitude);
    }
}
