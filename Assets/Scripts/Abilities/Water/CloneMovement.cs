using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class CloneMovement : MonoBehaviour
{
    [Header("Setup")]
    public Gamepad gamepad;       // Assigned from caster
    public float moveSpeed = 5f;
    public float gravity = -20f;
    public float jumpForce = 10f;
    public float dashSpeed = 20f;
    public float dashTime = 0.2f;

    private CharacterController controller;
    private Vector3 velocity;
    private bool isJumping = false;
    private bool isDashing = false;
    private float dashTimer = 0f;
    private Vector3 dashDir = Vector3.zero;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (gamepad == null) return;

        Vector2 rawInput = gamepad.leftStick.ReadValue();
        Vector3 inputDir = new Vector3(rawInput.x, 0f, -rawInput.y); // ← mirror vertical input

        Vector3 move = inputDir.normalized * moveSpeed;

        // Handle dash
        if (gamepad.buttonEast.wasPressedThisFrame && !isDashing)
        {
            dashDir = inputDir.normalized;
            dashTimer = dashTime;
            isDashing = true;
        }

        if (isDashing)
        {
            move = dashDir * dashSpeed;
            dashTimer -= Time.deltaTime;
            if (dashTimer <= 0f)
                isDashing = false;
        }

        // Handle jump
        if (controller.isGrounded)
        {
            velocity.y = -1f; // Keep grounded

            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                velocity.y = jumpForce;
            }
        }
        else
        {
            velocity.y += gravity * Time.deltaTime;
        }

        // Combine and move
        Vector3 finalMove = new Vector3(move.x, velocity.y, move.z);
        controller.Move(finalMove * Time.deltaTime);

        // Rotate toward move direction
        Vector3 flatMove = new Vector3(move.x, 0f, move.z);
        if (flatMove.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(flatMove);
    }
}
