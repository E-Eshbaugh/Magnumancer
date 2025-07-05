using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

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

    [Header("Slam")]
    public float slamPause = 0.2f;
    public float slamForce = 25f;

    [Header("Controller Settings")]
    [Tooltip("Set at runtime by MultiplayerManager")]
    public Gamepad gamepad = null;

    // internal state
    private CharacterController controller;
    private int jumpsRemaining;
    private bool slamming;
    private float verticalVelocity = 0f;
    private Vector3 lastDirection = Vector3.forward;
    private static readonly Quaternion IsoRotation = Quaternion.Euler(0, 45f, 0);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        jumpsRemaining = maxJumps;
    }

    void Update()
    {
        if (gamepad == null)
            return; // no pad? no movement

        // 1) Read stick
        Vector2 stick = gamepad.leftStick.ReadValue();
        // apply deadzone
        if (stick.magnitude < 0.1f) stick = Vector2.zero;

        // 2) Handle slam and jump inputs
        if (!slamming)
        {
            if (gamepad.buttonSouth.wasPressedThisFrame && (controller.isGrounded || jumpsRemaining > 0))
            {
                verticalVelocity = jumpForce;
                if (!controller.isGrounded)
                    jumpsRemaining--;
            }
            if (gamepad.buttonEast.wasPressedThisFrame && !controller.isGrounded)
            {
                StartCoroutine(DoSlam());
            }
        }

        // 3) Build movement vector
        Vector3 planar = new Vector3(stick.x, 0, stick.y).normalized;
        Vector3 isoDir = IsoRotation * planar;
        if (isoDir.sqrMagnitude > 0.01f && !slamming)
            lastDirection = isoDir;

        Vector3 horiz = lastDirection * moveSpeed * stick.magnitude;
        if (slamming) horiz = Vector3.zero;

        // 4) Gravity & jump reset
        if (controller.isGrounded)
        {
            if (verticalVelocity < 0f) verticalVelocity = -0.5f;
            jumpsRemaining = maxJumps - 1;
        }
        else verticalVelocity += gravity * Time.deltaTime;

        // 5) Move & rotate
        Vector3 motion = new Vector3(horiz.x, verticalVelocity, horiz.z);
        controller.Move(motion * Time.deltaTime);

        if (stick.sqrMagnitude > 0.01f && !slamming)
        {
            var target = Quaternion.LookRotation(lastDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, target, 10f * Time.deltaTime);
        }

        // 6) Animate
        if (animator != null)
            animator.SetFloat("Speed", stick.magnitude);
    }

    private IEnumerator DoSlam()
    {
        slamming = true;
        float oldVel = verticalVelocity;
        verticalVelocity = 0f;
        yield return new WaitForSeconds(slamPause);
        verticalVelocity = -slamForce;
        slamming = false;
    }
}
