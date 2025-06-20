using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public Animator animator;
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    public float verticalVelocity = 0f;
    public float jumpForce = 5f; // Optional: add jump later

    private Vector2 moveInput;
    private CharacterController controller;
    private static readonly Quaternion IsoRotation = Quaternion.Euler(0, 45f, 0);

    void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
    }

    void Update()
    {
        Vector3 input = new Vector3(moveInput.x, 0f, moveInput.y);
        Vector3 isoInput = IsoRotation * input;

        float moveMagnitude = isoInput.magnitude;

        // Set speed param for animation
        animator.SetFloat("Speed", moveMagnitude);

        // Apply gravity
        if (controller.isGrounded)
        {
            verticalVelocity = -0.5f; // Small downward push to stay grounded
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        // Combine horizontal and vertical movement
        Vector3 move = isoInput * moveSpeed + Vector3.up * verticalVelocity;

        // Move the player
        controller.Move(move * Time.deltaTime);

        // Rotate to face movement
        if (moveInput.sqrMagnitude > 0.01f)
        {
            Vector3 direction = IsoRotation * new Vector3(moveInput.x, 0f, moveInput.y);
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.deltaTime);
            }
        }
    }
}
